using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSink : ISinkPlugin
{
    public List<Notification> Received { get; } = [];
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public string Id { get; }
    public string Name => "Fake Sink";
    public string Version => "1.0.0";
    public string Author => "Test";
    public string Description => "Test sink";
    public string Icon => "";

    public FakeSink(string id = "com.test.sink")
    {
        Id = id;
    }

    public bool ThrowOnConsume { get; set; }

    public Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (ThrowOnConsume)
        {
            throw new InvalidOperationException("Simulated sink failure");
        }
        Received.Add(notification);

        return Task.CompletedTask;
    }

    public Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        Started = true;

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        Stopped = true;

        return Task.CompletedTask;
    }
}
