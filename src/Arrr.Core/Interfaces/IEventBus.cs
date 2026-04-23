namespace Arrr.Core.Interfaces;

/// <summary>In-process event bus for publishing and subscribing to Arrr events.</summary>
public interface IEventBus
{
    /// <summary>Publishes an event to all registered subscribers of type <typeparamref name="T"/>.</summary>
    Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : IArrrEvent;

    /// <summary>Registers a handler invoked for every event of type <typeparamref name="T"/>.</summary>
    void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent;
}
