using Arrr.Tests.Support;
using MimeKit;
using SmtpSink;
using SmtpSink.Data;
using SmtpSink.Types;

namespace Arrr.Tests.Sinks.Smtp;

[TestFixture]
public class SmtpSinkPluginTests
{
    private FakeMailSender _sender = null!;
    private SmtpSinkPlugin? _sink;

    [Test]
    public async Task ConsumeAsync_DigestMode_BatchesNotifications()
    {
        _sink = new(_sender, TimeSpan.FromMilliseconds(100));
        var ctx = new FakeSinkContext(
            configFactory: _ => new SmtpSinkConfig
            {
                Mode = SmtpDeliveryMode.Digest,
                From = "from@example.com",
                To = "to@example.com"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T1", "B1", DateTimeOffset.UtcNow, null),
            cts.Token
        );
        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T2", "B2", DateTimeOffset.UtcNow, null),
            cts.Token
        );
        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T3", "B3", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        await Task.Delay(400, cts.Token);

        Assert.That(_sender.Sent, Has.Count.EqualTo(1));
        var body = ((TextPart)_sender.Sent[0].Body).Text;
        Assert.That(body, Does.Contain("T1"));
        Assert.That(body, Does.Contain("T2"));
        Assert.That(body, Does.Contain("T3"));
    }

    [Test]
    public async Task ConsumeAsync_SingleMode_SendsOneEmailPerNotification()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new SmtpSinkConfig
            {
                Mode = SmtpDeliveryMode.Single,
                From = "from@example.com",
                To = "to@example.com"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "Title 1", "Body 1", DateTimeOffset.UtcNow, null),
            cts.Token
        );
        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "Title 2", "Body 2", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        Assert.That(_sender.Sent, Has.Count.EqualTo(2));
        Assert.That(_sender.Sent[0].Subject, Does.Contain("Title 1"));
        Assert.That(_sender.Sent[1].Subject, Does.Contain("Title 2"));
    }

    [SetUp]
    public void SetUp()
    {
        _sender = new();
        _sink = new(_sender);
    }

    [Test]
    public async Task StartAsync_WithValidConfig_DoesNotThrow()
    {
        var ctx = new FakeSinkContext(
            configFactory: _ => new SmtpSinkConfig
            {
                Host = "smtp.example.com",
                From = "from@example.com",
                To = "to@example.com"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink!.StartAsync(ctx, cts.Token);
    }

    [Test]
    public async Task StopAsync_DigestMode_FlushesRemainingNotifications()
    {
        _sink = new(_sender, TimeSpan.FromMinutes(60));
        var ctx = new FakeSinkContext(
            configFactory: _ => new SmtpSinkConfig
            {
                Mode = SmtpDeliveryMode.Digest,
                From = "from@example.com",
                To = "to@example.com"
            }
        );
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _sink.StartAsync(ctx, cts.Token);

        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T1", "B1", DateTimeOffset.UtcNow, null),
            cts.Token
        );
        await _sink.ConsumeAsync(
            new Notification(Guid.NewGuid(), "rss", "T2", "B2", DateTimeOffset.UtcNow, null),
            cts.Token
        );

        await _sink.StopAsync();
        _sink = null;

        Assert.That(_sender.Sent, Has.Count.EqualTo(1));
        var body = ((TextPart)_sender.Sent[0].Body).Text;
        Assert.That(body, Does.Contain("T1"));
        Assert.That(body, Does.Contain("T2"));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_sink is not null)
        {
            await _sink.StopAsync();
            _sink = null;
        }
    }
}
