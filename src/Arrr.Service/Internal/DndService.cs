using Arrr.Core.Interfaces;
using Arrr.Service.Interfaces;

namespace Arrr.Service.Internal;

internal sealed class DndService : IDndService
{
    private readonly IEventBus _eventBus;
    private volatile bool _enabled;

    public DndService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public bool IsEnabled => _enabled;

    public void Set(bool enabled)
    {
        _enabled = enabled;
        _ = _eventBus.PublishAsync(new DndChangedEvent(enabled, DateTimeOffset.UtcNow));
    }
}
