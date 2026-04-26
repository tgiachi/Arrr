using Arrr.Core.Data.Notifications;
using System.Net;
using Arrr.Tests.Support;
using HomeAssistantSink;
using HomeAssistantSink.Data;

namespace Arrr.Tests.Sinks.HomeAssistant;

[TestFixture]
public class HomeAssistantSinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private HomeAssistantSinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_SendsToCorrectEndpoint()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new HomeAssistantSinkConfig
            {
                BaseUrl = "http://ha.local:8123",
                AccessToken = "mytoken",
                NotifyService = "mobile_app_phone",
                TitleTemplate = "[{source}] {title}"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "imap", "New Email", "Hello world", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Not.Null);
        Assert.That(
            _handler.LastRequest!.RequestUri!.ToString(),
            Is.EqualTo("http://ha.local:8123/api/services/notify/mobile_app_phone")
        );
        Assert.That(
            _handler.LastRequest.Headers.Authorization!.ToString(),
            Is.EqualTo("Bearer mytoken")
        );

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain("[imap] New Email"));
        Assert.That(body, Does.Contain("Hello world"));
    }

    [Test]
    public async Task ConsumeAsync_WhenAccessTokenEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new HomeAssistantSinkConfig { AccessToken = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task ConsumeAsync_WhenHaReturnsError_DoesNotThrow()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Unauthorized;
        var ctx = new FakeSinkContext(
            configFactory: _ => new HomeAssistantSinkConfig
            {
                BaseUrl = "http://ha.local:8123",
                AccessToken = "bad-token"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => _sink.ConsumeAsync(notification, cts.Token));
    }

    [SetUp]
    public void SetUp()
    {
        _handler = new();
        _sink = new(_handler);
    }

    [Test]
    public async Task StartAsync_WithValidConfig_DoesNotThrow()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new HomeAssistantSinkConfig { AccessToken = "tok" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);
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
}
