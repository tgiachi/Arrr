using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeEventBus : IEventBus
{
    private readonly List<IArrrEvent> _published = [];
    public IReadOnlyList<IArrrEvent> Published => _published;

    public Task PublishAsync<T>(T evt, CancellationToken ct = default) where T : IArrrEvent
    {
        _published.Add(evt);
        return Task.CompletedTask;
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IArrrEvent { }
}
