using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Scans the plugins directory and manages enable/disable at runtime.</summary>
public interface IPluginManager
{
    /// <summary>Returns metadata for every plugin DLL found in the plugins directory.</summary>
    IReadOnlyList<AvailablePluginResponse> GetAvailable();

    /// <summary>Enables a plugin: persists to config and starts it.</summary>
    Task EnableAsync(string pluginId, CancellationToken ct);

    /// <summary>Disables a plugin: stops it and persists to config.</summary>
    Task DisableAsync(string pluginId);

    /// <summary>Unloads and reloads a single plugin by ID.</summary>
    Task ReloadAsync(string pluginId, CancellationToken ct);

    /// <summary>Unloads and reloads all plugins.</summary>
    Task ReloadAllAsync(CancellationToken ct);
}
