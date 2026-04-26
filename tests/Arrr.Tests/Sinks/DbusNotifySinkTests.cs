using Arrr.Core.Data.Notifications;
using Arrr.Service.Sinks;
using Arrr.Tests.Support;

namespace Arrr.Tests.Sinks;

[TestFixture]
public class DbusNotifySinkTests
{
    [Test]
    public async Task ConsumeAsync_WhenNotConnected_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        var ctx = new FakeSinkContext();

        using var cts = new CancellationTokenSource();
        await sink.StartAsync(ctx, cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "Hello", "World", DateTimeOffset.UtcNow, null);

        Assert.DoesNotThrowAsync(() => sink.ConsumeAsync(notification, cts.Token));

        await sink.StopAsync();
    }

    [Test]
    public async Task StartAsync_WhenSessionBusUnavailable_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        var ctx = new FakeSinkContext();

        using var cts = new CancellationTokenSource();
        Assert.DoesNotThrowAsync(() => sink.StartAsync(ctx, cts.Token));

        await sink.StopAsync();
    }

    [Test]
    public void StopAsync_WhenNeverStarted_DoesNotThrow()
    {
        var sink = new DbusNotifySink();
        Assert.DoesNotThrowAsync(() => sink.StopAsync());
    }
}
