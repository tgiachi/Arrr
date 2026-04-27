using Arrr.Service.Interfaces;

namespace Arrr.Service.Internal;

internal sealed class DndService : IDndService
{
    private volatile bool _enabled;

    public bool IsEnabled => _enabled;

    public void Set(bool enabled)
    {
        _enabled = enabled;
    }
}
