using Arrr.Core.Interfaces;

namespace Arrr.Service.Services;

public class PluginRegistryService : IPluginRegistry
{
    private readonly Dictionary<string, ISourcePlugin> _plugins = new();
    private readonly Lock _lock = new();

    public void Register(ISourcePlugin plugin)
    {
        lock (_lock)
        {
            _plugins[plugin.Id] = plugin;
        }
    }

    public void Unregister(string pluginId)
    {
        lock (_lock)
        {
            _plugins.Remove(pluginId);
        }
    }

    public IReadOnlyList<ISourcePlugin> GetAll()
    {
        lock (_lock)
        {
            return _plugins.Values.ToList();
        }
    }
}
