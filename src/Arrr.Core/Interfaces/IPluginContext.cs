using Microsoft.Extensions.Logging;

namespace Arrr.Core.Interfaces;

/// <summary>Runtime context injected into each plugin at startup.</summary>
public interface IPluginContext
{
    /// <summary>Path to the plugin's dedicated config file ({pluginId}.config).</summary>
    string ConfigPath { get; }

    /// <summary>Logger scoped to this plugin, writing to logs/plugins/{pluginId}.log.</summary>
    ILogger Logger { get; }

    /// <summary>HTTP callback URL for this plugin (/callback/{pluginName}).</summary>
    string CallbackUrl { get; }

    /// <summary>Event bus for publishing notifications and other events.</summary>
    IEventBus EventBus { get; }

    /// <summary>
    /// Deserializes the plugin config from <see cref="ConfigPath" />.
    /// Properties marked with <c>[Sensitive]</c> are decrypted automatically.
    /// Returns a default instance if the file does not exist.
    /// </summary>
    Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new();

    /// <summary>
    /// Serializes <paramref name="config" /> to <see cref="ConfigPath" />.
    /// Properties marked with <c>[Sensitive]</c> are encrypted automatically before writing.
    /// </summary>
    Task SaveConfigAsync<T>(T config, CancellationToken ct = default);
}
