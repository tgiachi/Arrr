using System.Reflection;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly string _pluginsPath;

    public PluginLoader(ILogger<PluginLoader> logger, string pluginsPath)
    {
        _logger = logger;
        _pluginsPath = pluginsPath;
    }

    public IReadOnlyList<ISourcePlugin> Load()
    {
        var plugins = new List<ISourcePlugin>();

        if (!Directory.Exists(_pluginsPath))
        {
            _logger.LogWarning("Plugins directory not found: {Path}", _pluginsPath);

            return plugins;
        }

        foreach (var file in Directory.EnumerateFiles(_pluginsPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                var types = assembly.GetTypes()
                                    .Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is ISourcePlugin plugin)
                    {
                        plugins.Add(plugin);
                        _logger.LogInformation("Loaded plugin: {Name} from {File}", plugin.Name, Path.GetFileName(file));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {File}", Path.GetFileName(file));
            }
        }

        return plugins;
    }
}
