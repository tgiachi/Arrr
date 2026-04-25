using Arrr.Core.Data.Notifications;
using Arrr.Tests.Support;
using WebhookSink;
using WebhookSink.Data;

namespace Arrr.Tests.Sinks.Webhook;

[TestFixture]
public class WebhookSinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private WebhookSinkPlugin?     _sink;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeHttpMessageHandler();
        _sink    = new WebhookSinkPlugin(_handler);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_sink is not null)
        {
            await _sink.StopAsync();
            _sink = null;
        }
        _handler.Dispose();
    }

    [Test]
    public async Task StartAsync_WithValidConfig_DoesNotThrow()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebhookSinkConfig { Url = "https://example.com/hook" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);
    }

    [Test]
    public async Task ConsumeAsync_PostsJsonNotification()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebhookSinkConfig
        {
            Url       = "https://example.com/hook",
            AuthToken = "secret-token"
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Test Title", "Test Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest,                                         Is.Not.Null);
        Assert.That(_handler.LastRequest!.RequestUri!.ToString(),                 Is.EqualTo("https://example.com/hook"));
        Assert.That(_handler.LastRequest.Method,                                  Is.EqualTo(HttpMethod.Post));
        Assert.That(_handler.LastRequest.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
        Assert.That(_handler.LastRequest.Headers.Authorization,                   Is.Not.Null);
        Assert.That(_handler.LastRequest.Headers.Authorization!.Scheme,           Is.EqualTo("Bearer"));
        Assert.That(_handler.LastRequest.Headers.Authorization!.Parameter,        Is.EqualTo("secret-token"));

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain("Test Title"));
        Assert.That(body, Does.Contain("Test Body"));
        Assert.That(body, Does.Contain("rss"));
    }

    [Test]
    public async Task ConsumeAsync_WhenUrlEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new WebhookSinkConfig { Url = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
    }
}
