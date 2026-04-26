using Arrr.Core.Data.Notifications;
using Arrr.Core.Services;
using Arrr.Service.Internal;
using Arrr.Tests.Support;

namespace Arrr.Tests.Service;

[TestFixture]
public class SinkOrchestratorTests
{
    private EventBusService _bus = null!;
    private CancellationTokenSource _cts = null!;

    [Test]
    public async Task Notification_DeliveredToAllRunningSinks()
    {
        var sinkA = new FakeSink("com.test.a");
        var sinkB = new FakeSink("com.test.b");

        var orchestrator = new SinkOrchestrator(_bus);
        orchestrator.AddSinkForTest(sinkA);
        orchestrator.AddSinkForTest(sinkB);
        await orchestrator.StartSinksForTestAsync(_cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        await Task.Delay(100, _cts.Token);

        Assert.That(sinkA.Received, Has.Count.EqualTo(1));
        Assert.That(sinkB.Received, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Notification_WhenOneSinkThrows_OtherSinkStillReceives()
    {
        var throwingSink = new FakeSink("com.test.throw") { ThrowOnConsume = true };
        var goodSink = new FakeSink("com.test.good");

        var orchestrator = new SinkOrchestrator(_bus);
        orchestrator.AddSinkForTest(throwingSink);
        orchestrator.AddSinkForTest(goodSink);
        await orchestrator.StartSinksForTestAsync(_cts.Token);

        var notification = new Notification(Guid.NewGuid(), "test", "T", "B", DateTimeOffset.UtcNow, null);
        await _bus.PublishAsync(notification, _cts.Token);

        await Task.Delay(100, _cts.Token);

        Assert.That(goodSink.Received, Has.Count.EqualTo(1));
    }

    [SetUp]
    public async Task SetUp()
    {
        _bus = new EventBusService();
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
