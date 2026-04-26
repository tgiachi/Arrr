using Arrr.Core.Data.Notifications;
using Arrr.Core.Types;
using System.Net;
using Arrr.Tests.Support;
using NtfySink;
using NtfySink.Data;

namespace Arrr.Tests.Sinks.Ntfy;

[TestFixture]
public class NtfySinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private NtfySinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_SendsCorrectHttpRequest()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new NtfySinkConfig
            {
                ServerUrl = "https://ntfy.example.com",
                Topic = "alerts",
                TitleTemplate = "[{source}] {title}"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(
            Guid.NewGuid(),
            "rss",
            "New Article",
            "Article body",
            DateTimeOffset.UtcNow,
            null,
            Priority: NotificationPriority.High
        );
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Not.Null);
        Assert.That(_handler.LastRequest!.RequestUri!.ToString(), Is.EqualTo("https://ntfy.example.com/alerts"));
        Assert.That(_handler.LastRequest.Headers.GetValues("X-Title").First(), Is.EqualTo("[rss] New Article"));
        Assert.That(_handler.LastRequest.Headers.GetValues("X-Priority").First(), Is.EqualTo("high")); // High → "high"

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.That(body, Is.EqualTo("Article body"));
    }

    [Test]
    public async Task ConsumeAsync_WhenServerReturnsError_DoesNotThrow()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        var ctx = new FakeSinkContext(
            configFactory: _ => new NtfySinkConfig
            {
                ServerUrl = "https://ntfy.example.com",
                Topic = "alerts"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Title", "Body", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => _sink.ConsumeAsync(notification, cts.Token));
    }

    [Test]
    public async Task ConsumeAsync_WhenTopicEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new NtfySinkConfig { Topic = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "rss", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Null);
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
        var ctx = new FakeSinkContext(configFactory: _ => new NtfySinkConfig { Topic = "test" });
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
