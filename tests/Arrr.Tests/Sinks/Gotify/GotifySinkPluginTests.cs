using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using System.Net;
using Arrr.Tests.Support;
using GotifySink;
using GotifySink.Data;

namespace Arrr.Tests.Sinks.Gotify;

[TestFixture]
public class GotifySinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private GotifySinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_SendsCorrectRequest()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new GotifySinkConfig
            {
                ServerUrl = "http://gotify.example.com",
                AppToken = "mytoken",
                TitleTemplate = "[{source}] {title}"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(
            Guid.NewGuid(),
            "rss",
            "New Post",
            "Body text",
            DateTimeOffset.UtcNow,
            null,
            Priority: NotificationPriority.High
        );
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Not.Null);
        Assert.That(_handler.LastRequest!.RequestUri!.ToString(), Is.EqualTo("http://gotify.example.com/message"));
        Assert.That(_handler.LastRequest.Headers.GetValues("X-Gotify-Key").First(), Is.EqualTo("mytoken"));

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain("[rss] New Post"));
        Assert.That(body, Does.Contain("Body text"));
        Assert.That(body, Does.Contain("8")); // High → 8
    }

    [Test]
    public async Task ConsumeAsync_WhenAppTokenEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new GotifySinkConfig { AppToken = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task ConsumeAsync_WhenServerReturnsError_DoesNotThrow()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        var ctx = new FakeSinkContext(
            configFactory: _ => new GotifySinkConfig
            {
                ServerUrl = "http://gotify.example.com",
                AppToken = "tok"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Title", "Body", DateTimeOffset.UtcNow, null);
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
        var ctx = new FakeSinkContext(configFactory: _ => new GotifySinkConfig { AppToken = "tok" });
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
