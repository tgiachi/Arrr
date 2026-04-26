using System.Net;
using Arrr.Sink.Pushover;
using Arrr.Sink.Pushover.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks.Pushover;

[TestFixture]
public class PushoverSinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private PushoverSinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_IncludesSoundField_WhenConfigured()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new PushoverConfig
            {
                ApiToken = "tok",
                UserKey = "usr",
                Sound = "magic"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain("sound=magic"));
    }

    [Test]
    public async Task ConsumeAsync_SendsFormFieldsToCorrectUrl()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new PushoverConfig
            {
                ApiToken = "APP_TOKEN",
                UserKey = "USER_KEY",
                Priority = 1
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(
            Guid.NewGuid(),
            "rss",
            "New Article",
            "Some body text",
            DateTimeOffset.UtcNow,
            null
        );
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Not.Null);
        Assert.That(_handler.LastRequest!.RequestUri!.ToString(), Is.EqualTo("https://api.pushover.net/1/messages.json"));

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        Assert.That(body, Does.Contain("APP_TOKEN"));
        Assert.That(body, Does.Contain("USER_KEY"));
        Assert.That(body, Does.Contain("New+Article").Or.Contain("New%20Article").Or.Contain("New Article"));
        Assert.That(body, Does.Contain("priority=1"));
    }

    [Test]
    public async Task ConsumeAsync_WhenServerReturnsError_DoesNotThrow()
    {
        _handler.ResponseStatusCode = HttpStatusCode.BadRequest;
        var ctx = new FakeSinkContext(configFactory: _ => new PushoverConfig { ApiToken = "tok", UserKey = "usr" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        Assert.DoesNotThrowAsync(
            () => _sink.ConsumeAsync(
                new Notification(Guid.NewGuid(), "rss", "T", "B", DateTimeOffset.UtcNow, null),
                cts.Token
            )
        );
    }

    [Test]
    public async Task ConsumeAsync_WhenTokenEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new PushoverConfig { ApiToken = "", UserKey = "usr" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T", "B", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [SetUp]
    public void SetUp()
    {
        _handler = new();
        _sink = new(_handler);
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
