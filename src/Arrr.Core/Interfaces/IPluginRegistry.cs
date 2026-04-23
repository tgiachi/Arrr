namespace Arrr.Core.Interfaces;

/// <summary>Registry of currently active plugins, used for HTTP callback dispatch.</summary>
public interface IPluginRegistry
{
    /// <summary>Registers a plugin as active.</summary>
    void Register(ISourcePlugin plugin);

    /// <summary>Removes a plugin by its Id.</summary>
    void Unregister(string pluginId);

    /// <summary>Returns all active plugins.</summary>
    IReadOnlyList<ISourcePlugin> GetAll();
}
