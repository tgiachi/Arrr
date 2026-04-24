using System.Text.Json;
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

    /// <summary>
    /// Returns the plugin's config as a <see cref="JsonElement"/> with sensitive fields decrypted.
    /// Returns <c>null</c> if the plugin does not implement <see cref="IConfigurablePlugin"/>
    /// or its DLL has not been loaded.
    /// </summary>
    Task<JsonElement?> GetPluginConfigAsync(string pluginId, CancellationToken ct = default);

    /// <summary>
    /// Persists <paramref name="config"/> to the plugin's config file,
    /// encrypting any <c>[Sensitive]</c> fields before writing.
    /// </summary>
    Task SavePluginConfigAsync(string pluginId, JsonElement config, CancellationToken ct = default);
}
