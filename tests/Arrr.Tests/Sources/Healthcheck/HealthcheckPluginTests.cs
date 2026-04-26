using System.Net;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using Arrr.Plugin.Healthcheck;
using Arrr.Plugin.Healthcheck.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sources.Healthcheck;

[TestFixture]
public class HealthcheckPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private FakeEventBus _eventBus = null!;
    private HealthcheckPlugin _plugin = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeHttpMessageHandler();
        _eventBus = new FakeEventBus();
        _plugin = new HealthcheckPlugin(_handler, new FakeTimeProvider(DateTimeOffset.UtcNow));
    }

    [TearDown]
    public void TearDown()
    {
        _plugin.Dispose();
        _handler.Dispose();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private FakePluginContext CtxWith(HealthcheckConfig config)
        => new(_eventBus, _ => config);

    private static HealthcheckConfig OneEndpoint(
        string url = "http://example.com",
        string name = "Example",
        List<int>? expectedCodes = null)
        => new()
        {
            Endpoints =
            [
                new EndpointConfig { Url = url, Name = name, ExpectedStatusCodes = expectedCodes ?? [] }
            ],
            PollIntervalSeconds = 60,
            TimeoutSeconds = 5
        };

    private async Task StartAndPoll(HealthcheckConfig config, CancellationToken ct = default)
    {
        var ctx = CtxWith(config);
        await _plugin.StartAsync(ctx, ct);
        await _plugin.PollAsync(ctx, ct);
    }

    private Notification Published(int index = 0)
        => (Notification)_eventBus.Published[index];

    // ── no endpoints ─────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_DoesNotPublish_WhenNoEndpoints()
    {
        await StartAndPoll(new HealthcheckConfig { Endpoints = [] });

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── first probe: baseline silently ───────────────────────────────────────

    [Test]
    public async Task PollAsync_DoesNotPublish_OnFirstProbe_WhenUp()
    {
        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await StartAndPoll(OneEndpoint());

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_OnFirstProbe_WhenDown()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        await StartAndPoll(OneEndpoint());

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── state changes ─────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_PublishesCritical_WhenEndpointGoesDown()
    {
        var config = OneEndpoint();
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline: up

        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        await _plugin.PollAsync(ctx, cts.Token); // up → down

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        Assert.That(Published().Priority, Is.EqualTo(NotificationPriority.Critical));
        Assert.That(Published().Title, Does.Contain("down"));
    }

    [Test]
    public async Task PollAsync_PublishesNormal_WhenEndpointRecovers()
    {
        var config = OneEndpoint();
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline: down

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.PollAsync(ctx, cts.Token); // down → up

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        Assert.That(Published().Priority, Is.EqualTo(NotificationPriority.Normal));
        Assert.That(Published().Title, Does.Contain("recovered"));
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_WhenStatusUnchanged_Up()
    {
        var config = OneEndpoint();
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline
        await _plugin.PollAsync(ctx, cts.Token); // still up

        Assert.That(_eventBus.Published, Is.Empty);
    }

    [Test]
    public async Task PollAsync_DoesNotPublish_WhenStatusUnchanged_Down()
    {
        var config = OneEndpoint();
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline
        await _plugin.PollAsync(ctx, cts.Token); // still down

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── timeout ───────────────────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_PublishesCritical_WhenEndpointTimesOut()
    {
        var config = OneEndpoint();
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline: up

        _handler.ShouldTimeout = true;
        await _plugin.PollAsync(ctx, cts.Token); // timeout → down

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        Assert.That(Published().Priority, Is.EqualTo(NotificationPriority.Critical));
        Assert.That(Published().Body, Does.Contain("timed out"));
    }

    // ── custom expected status codes ──────────────────────────────────────────

    [Test]
    public async Task PollAsync_TreatsResponseAsDown_WhenCodeNotInCustomList()
    {
        var config = OneEndpoint(expectedCodes: [301, 302]);
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.MovedPermanently; // 301 — healthy
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline: up

        _handler.ResponseStatusCode = HttpStatusCode.OK; // 200 — NOT in custom list → down
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        Assert.That(Published().Priority, Is.EqualTo(NotificationPriority.Critical));
    }

    [Test]
    public async Task PollAsync_UsesDefaultRange_WhenExpectedCodesEmpty()
    {
        var config = OneEndpoint(expectedCodes: []); // empty → 200–299
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline: up

        _handler.ResponseStatusCode = HttpStatusCode.NotFound; // 404 — outside default range
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        Assert.That(Published().Priority, Is.EqualTo(NotificationPriority.Critical));
    }

    // ── notification content ──────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_UsesEndpointName_InTitle()
    {
        var config = OneEndpoint(name: "My API");
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline

        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        await _plugin.PollAsync(ctx, cts.Token);

        Assert.That(Published().Title, Does.Contain("My API"));
    }

    [Test]
    public async Task PollAsync_IncludesExtras_WithUrlAndUpStatus()
    {
        const string url = "http://example.com/health";
        var config = OneEndpoint(url: url);
        var ctx = CtxWith(config);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _handler.ResponseStatusCode = HttpStatusCode.OK;
        await _plugin.StartAsync(ctx, cts.Token);
        await _plugin.PollAsync(ctx, cts.Token); // baseline

        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        await _plugin.PollAsync(ctx, cts.Token);

        var extras = Published().Extras;
        Assert.That(extras, Contains.Key("healthcheck.url"));
        Assert.That(extras!["healthcheck.url"], Is.EqualTo(url));
        Assert.That(extras["healthcheck.up"], Is.EqualTo("false"));
    }
}
