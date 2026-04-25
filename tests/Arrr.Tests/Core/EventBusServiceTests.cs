using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;

namespace Arrr.Tests.Core;

[TestFixture]
public class EventBusServiceTests
{
    private EventBusService _bus = null!;
    private CancellationTokenSource _cts = null!;

    [Test]
    public async Task PublishAsync_WhenMultipleSubscribers_AllHandlersInvoked()
    {
        var count = 0;
        _bus.Subscribe<Notification>(
            (_, _) =>
            {
                Interlocked.Increment(ref count);

                return Task.CompletedTask;
            }
        );
        _bus.Subscribe<Notification>(
            (_, _) =>
            {
                Interlocked.Increment(ref count);

                return Task.CompletedTask;
            }
        );

        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        await Task.Delay(100, _cts.Token);
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task PublishAsync_WhenNoSubscribers_DoesNotThrow()
    {
        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        Assert.DoesNotThrowAsync(() => _bus.PublishAsync(notification, _cts.Token));
        await Task.CompletedTask;
    }

    [Test]
    public async Task PublishAsync_WhenSubscriberRegistered_HandlerInvoked()
    {
        var received = new TaskCompletionSource<Notification>();
        _bus.Subscribe<Notification>(
            (n, ct) =>
            {
                received.TrySetResult(n);

                return Task.CompletedTask;
            }
        );

        var notification = new Notification(Guid.NewGuid(), "test", "Title", "Body", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result, Is.EqualTo(notification));
    }

    [SetUp]
    public async Task SetUp()
    {
        _bus = new();
        _cts = new(TimeSpan.FromSeconds(3));
        await _bus.StartAsync(_cts.Token);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _bus.StopAsync(_cts.Token);
        _cts.Dispose();
    }
}
