using System.Net;
using System.Text.Json;
using Arrr.Sink.Bark;
using Arrr.Sink.Bark.Data;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks.Bark;

[TestFixture]
public class BarkSinkPluginTests
{
    private FakeHttpMessageHandler _handler = null!;
    private BarkSinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_IncludesSound_WhenConfigured()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new BarkConfig { DeviceKey = "KEY", Sound = "minuet" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.That(doc.RootElement.GetProperty("sound").GetString(), Is.EqualTo("minuet"));
    }

    [Test]
    public async Task ConsumeAsync_PostsJsonToCorrectUrl()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new BarkConfig
            {
                ServerUrl = "https://api.day.app",
                DeviceKey = "DEVICE123",
                Level = "active",
                Group = "Arrr"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        var notification = new Notification(
            Guid.NewGuid(),
            "rss",
            "Breaking News",
            "Details here",
            DateTimeOffset.UtcNow,
            null
        );
        await _sink.ConsumeAsync(notification, cts.Token);

        Assert.That(_handler.LastRequest, Is.Not.Null);
        Assert.That(_handler.LastRequest!.RequestUri!.ToString(), Is.EqualTo("https://api.day.app/push"));

        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.That(doc.RootElement.GetProperty("device_key").GetString(), Is.EqualTo("DEVICE123"));
        Assert.That(doc.RootElement.GetProperty("title").GetString(), Is.EqualTo("Breaking News"));
        Assert.That(doc.RootElement.GetProperty("body").GetString(), Is.EqualTo("Details here"));
        Assert.That(doc.RootElement.GetProperty("level").GetString(), Is.EqualTo("active"));
        Assert.That(doc.RootElement.GetProperty("group").GetString(), Is.EqualTo("Arrr"));
    }

    [Test]
    public async Task ConsumeAsync_UsesSelfHostedServer_WhenConfigured()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new BarkConfig
            {
                ServerUrl = "https://bark.myserver.com",
                DeviceKey = "KEY"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        Assert.That(_handler.LastRequest!.RequestUri!.ToString(), Is.EqualTo("https://bark.myserver.com/push"));
    }

    [Test]
    public async Task ConsumeAsync_WhenDeviceKeyEmpty_DoesNotSend()
    {
        var ctx = new FakeSinkContext(configFactory: _ => new BarkConfig { DeviceKey = "" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T", "B", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        Assert.That(_handler.LastRequest, Is.Null);
    }

    [Test]
    public async Task ConsumeAsync_WhenServerReturnsError_DoesNotThrow()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        var ctx = new FakeSinkContext(configFactory: _ => new BarkConfig { DeviceKey = "KEY" });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        Assert.DoesNotThrowAsync(
            () => _sink.ConsumeAsync(
                new Notification(Guid.NewGuid(), "rss", "T", "B", DateTimeOffset.UtcNow, null),
                cts.Token
            )
        );
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
