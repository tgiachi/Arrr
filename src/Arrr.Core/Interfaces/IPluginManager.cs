using System.Text.Json;
using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Scans the plugins directory and manages enable/disable at runtime.</summary>
public interface IPluginManager
{
    /// <summary>
    /// Delivers an arbitrary payload to a running plugin that implements <see cref="ICallbackPlugin" />.
    /// Throws <see cref="KeyNotFoundException" /> if the plugin is not running or does not support callbacks.
    /// </summary>
    Task DeliverCallbackAsync(string pluginId, string body, CancellationToken ct = default);

    /// <summary>Disables a plugin: stops it and persists to config.</summary>
    Task DisableAsync(string pluginId);

    /// <summary>Enables a plugin: persists to config and starts it.</summary>
    Task EnableAsync(string pluginId, CancellationToken ct);

    /// <summary>Returns metadata for every plugin DLL found in the plugins directory.</summary>
    IReadOnlyList<AvailablePluginResponse> GetAvailable();

    /// <summary>
    /// Returns the raw PNG bytes of the icon embedded in the plugin's DLL,
    /// or <c>null</c> if the plugin has no embedded <c>icon.png</c> resource.
    /// </summary>
    byte[]? GetPluginIcon(string pluginId);

    /// <summary>
    /// Returns the pending QR code string for a running plugin that implements <see cref="IQrPlugin" />,
    /// or <c>null</c> if no pairing is currently in progress.
    /// </summary>
    string? GetPendingQrCode(string pluginId);

    /// <summary>
    /// Returns the plugin's config values (sensitive fields decrypted) plus the field schema
    /// (name, description, sensitive flag) derived from the config type's properties.
    /// Returns <c>null</c> if the plugin does not implement <see cref="IConfigurablePlugin" />.
    /// </summary>
    Task<PluginConfigResponse?> GetPluginConfigAsync(string pluginId, CancellationToken ct = default);

    /// <summary>Unloads and reloads all plugins.</summary>
    Task ReloadAllAsync(CancellationToken ct);

    /// <summary>Unloads and reloads a single plugin by ID.</summary>
    Task ReloadAsync(string pluginId, CancellationToken ct);

    /// <summary>
    /// Persists <paramref name="config" /> to the plugin's config file,
    /// encrypting any <c>[Sensitive]</c> fields before writing.
    /// </summary>
    Task SavePluginConfigAsync(string pluginId, JsonElement config, CancellationToken ct = default);

    /// <summary>
    /// Instantiates the plugin with the provided <paramref name="config" /> (without persisting it)
    /// and calls <see cref="ITestablePlugin.TestAsync" />.
    /// Returns <c>null</c> if the plugin does not implement <see cref="ITestablePlugin" />.
    /// Throws <see cref="KeyNotFoundException" /> if the plugin DLL is not found.
    /// </summary>
    Task<PluginTestResult?> TestPluginAsync(string pluginId, JsonElement config, CancellationToken ct = default);
}
