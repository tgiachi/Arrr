using Arrr.Core.Services;
using Arrr.Service.Subscribers;

namespace Arrr.Tests.Service;

[TestFixture]
public class DBusNotifySubscriberTests
{
    [Test]
    public async Task StartAsync_WhenSessionBusUnavailable_DoesNotThrow()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var subscriber = new DBusNotifySubscriber(bus);

        Assert.DoesNotThrowAsync(() => subscriber.StartAsync(cts.Token));

        await subscriber.StopAsync(cts.Token);
        await bus.StopAsync(cts.Token);
    }

    [Test]
    public async Task StopAsync_WhenNeverConnected_DoesNotThrow()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        var subscriber = new DBusNotifySubscriber(bus);

        Assert.DoesNotThrowAsync(() => subscriber.StopAsync(cts.Token));
    }
}
