using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class PollingPluginTests
{
    [Test]
    public async Task PollAsync_Cancellation_StopsLoop()
    {
        var plugin = new FakePollingPlugin(TimeSpan.FromMilliseconds(10));
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var ctx = new FakePluginContext(bus);
        var loop = RunPollingLoopAsync(plugin, ctx, cts.Token);

        await Task.Delay(30);
        await cts.CancelAsync();
        await loop;

        var countAtStop = plugin.PollCount;
        await Task.Delay(50);

        Assert.That(
            plugin.PollCount,
            Is.EqualTo(countAtStop),
            "no more polls must happen after cancellation"
        );

        await bus.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task PollAsync_ExceptionOnFirstCall_LoopContinues()
    {
        var plugin = new FakePollingPlugin(
            TimeSpan.FromMilliseconds(10),
            call => call == 1 ? new InvalidOperationException("boom") : null
        );
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var ctx = new FakePluginContext(bus);
        var loop = RunPollingLoopAsync(plugin, ctx, cts.Token);

        await Task.Delay(60);
        await cts.CancelAsync();

        await loop;

        Assert.That(
            plugin.PollCount,
            Is.GreaterThanOrEqualTo(2),
            "loop must continue after a poll exception"
        );

        await bus.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task PollAsync_IsCalledMultipleTimes_BeforeCancellation()
    {
        var plugin = new FakePollingPlugin(TimeSpan.FromMilliseconds(10));
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource();
        await bus.StartAsync(cts.Token);

        var ctx = new FakePluginContext(bus);
        var loop = RunPollingLoopAsync(plugin, ctx, cts.Token);

        await Task.Delay(80);
        await cts.CancelAsync();

        await loop;

        Assert.That(plugin.PollCount, Is.GreaterThanOrEqualTo(3));

        await bus.StopAsync(CancellationToken.None);
    }

    // Simulates the loop the orchestrator runs for IPollingPlugin — keeps tests
    // independent from the full BackgroundService/DLL-loading machinery.
    private static Task RunPollingLoopAsync(IPollingPlugin plugin, IPluginContext ctx, CancellationToken ct)
        => Task.Run(
            async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await plugin.PollAsync(ctx, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    catch
                    {
                        /* host swallows plugin errors */
                    }

                    await Task.Delay(plugin.Interval, ct)
                              .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                }
            },
            ct
        );
}
