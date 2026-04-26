namespace Arrr.Tests.Core;

[TestFixture]
public class ArrStartedEventTests
{
    [Test]
    public async Task ArrStartedEvent_Body_ContainsConfiguredPort()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await bus.StartAsync(cts.Token);

        var config = new ArrrConfig { Web = new() { Port = 9090 } };
        var received = new TaskCompletionSource<Notification>();

        bus.Subscribe<ArrStartedEvent>(
            async (evt, token) =>
                await bus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        "arrr",
                        "Arrr started",
                        $"Listening on port {config.Web.Port}",
                        evt.Timestamp,
                        null
                    ),
                    token
                )
        );
        bus.Subscribe<Notification>(
            (n, _) =>
            {
                received.TrySetResult(n);

                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(new ArrStartedEvent(DateTimeOffset.UtcNow), cts.Token);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.That(notification.Body, Does.Contain("9090"));

        await bus.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ArrStartedEvent_WhenPublished_ProducesNotificationWithSourceArrr()
    {
        var bus = new EventBusService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await bus.StartAsync(cts.Token);

        var config = new ArrrConfig { Web = new() { Port = 5150 } };
        var received = new TaskCompletionSource<Notification>();

        // same subscription logic as in Program.cs
        bus.Subscribe<ArrStartedEvent>(
            async (evt, token) =>
                await bus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        "arrr",
                        "Arrr started",
                        $"Listening on port {config.Web.Port}",
                        evt.Timestamp,
                        null
                    ),
                    token
                )
        );
        bus.Subscribe<Notification>(
            (n, _) =>
            {
                received.TrySetResult(n);

                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(new ArrStartedEvent(DateTimeOffset.UtcNow), cts.Token);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.That(notification.Source, Is.EqualTo("arrr"));
        Assert.That(notification.Title, Is.EqualTo("Arrr started"));
        Assert.That(notification.Body, Is.EqualTo("Listening on port 5150"));

        await bus.StopAsync(CancellationToken.None);
    }
}
