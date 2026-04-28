using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal sealed class NullEventBus : IEventBus
{
    public static readonly NullEventBus Instance = new();

    private NullEventBus() { }

    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IArrrEvent
        => Task.CompletedTask;

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent { }
}
