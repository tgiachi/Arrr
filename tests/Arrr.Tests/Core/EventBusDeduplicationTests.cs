using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Tests.Support;

namespace Arrr.Tests.Core;

[TestFixture]
public class EventBusDeduplicationTests
{
    private EventBusService _bus = null!;
    private FakeConfigService _config = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public async Task SetUp()
    {
        _config = new FakeConfigService();
        _bus = new(_config);
        _cts = new(TimeSpan.FromSeconds(3));
        await _bus.StartAsync(_cts.Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _bus.StopAsync(_cts.Token);
        _cts.Dispose();
    }

    [Test]
    public async Task PublishAsync_WhenDeduplicationDisabled_DuplicateIsDelivered()
    {
        _config.Config.Deduplication = new() { Enabled = false };
        var count = 0;
        _bus.Subscribe<Notification>((_, _) => { count++; return Task.CompletedTask; });

        var n = new Notification(Guid.NewGuid(), "src", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(n, _cts.Token);
        await _bus.PublishAsync(n with { Id = Guid.NewGuid() }, _cts.Token);

        await Task.Delay(150, _cts.Token);
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task PublishAsync_WhenDeduplicationEnabled_DuplicateWithinWindowIsDropped()
    {
        _config.Config.Deduplication = new() { Enabled = true, WindowSeconds = 60 };
        var count = 0;
        _bus.Subscribe<Notification>((_, _) => { count++; return Task.CompletedTask; });

        var n = new Notification(Guid.NewGuid(), "src", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(n, _cts.Token);
        await _bus.PublishAsync(n with { Id = Guid.NewGuid() }, _cts.Token);

        await Task.Delay(150, _cts.Token);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task PublishAsync_WhenDeduplicationEnabled_DifferentBodyIsNotDropped()
    {
        _config.Config.Deduplication = new() { Enabled = true, WindowSeconds = 60 };
        var count = 0;
        _bus.Subscribe<Notification>((_, _) => { count++; return Task.CompletedTask; });

        var n1 = new Notification(Guid.NewGuid(), "src", "Title", "Body A", DateTimeOffset.UtcNow, null);
        var n2 = new Notification(Guid.NewGuid(), "src", "Title", "Body B", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(n1, _cts.Token);
        await _bus.PublishAsync(n2, _cts.Token);

        await Task.Delay(150, _cts.Token);
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task PublishAsync_WhenDeduplicationEnabled_NotificationAfterWindowIsDelivered()
    {
        _config.Config.Deduplication = new() { Enabled = true, WindowSeconds = 0 };
        var count = 0;
        _bus.Subscribe<Notification>((_, _) => { count++; return Task.CompletedTask; });

        var n = new Notification(Guid.NewGuid(), "src", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(n, _cts.Token);
        await Task.Delay(50, _cts.Token);
        await _bus.PublishAsync(n with { Id = Guid.NewGuid() }, _cts.Token);

        await Task.Delay(150, _cts.Token);
        Assert.That(count, Is.EqualTo(2));
    }
}
