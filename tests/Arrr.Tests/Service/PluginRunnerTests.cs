using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PluginRunnerTests
{
    [Test]
    public async Task StartAsync_WhenPluginPublishesNotification_EventBusReceivesIt()
    {
        var expected = new Notification(Guid.NewGuid(), "fake", "Title", "Body", DateTimeOffset.UtcNow, null);
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await bus.StartAsync(cts.Token);

        var received = new TaskCompletionSource<Notification>();
        bus.Subscribe<Notification>(
            (n, _) =>
            {
                received.TrySetResult(n);

                return Task.CompletedTask;
            }
        );

        var plugin = new FakeSourcePlugin("com.test.plugin", [expected]);
        var ctx = new FakePluginContext(bus);
        await plugin.StartAsync(ctx, cts.Token);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(result, Is.EqualTo(expected));

        await bus.StopAsync(cts.Token);
    }

    [Test]
    public async Task StartAsync_WhenPluginThrows_ExceptionPropagates()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var plugin = new FakeSourcePlugin("com.test.broken", throws: new InvalidOperationException("boom"));
        var ctx = new FakePluginContext(bus);

        Assert.ThrowsAsync<InvalidOperationException>(() => plugin.StartAsync(ctx, cts.Token));

        await bus.StopAsync(cts.Token);
    }
}
