namespace Arrr.Core.Interfaces;

/// <summary>
/// Contract for notification source plugins.
/// Each plugin connects to an external source and publishes notifications via IPluginContext.EventBus.
/// </summary>
public interface ISourcePlugin
{
    /// <summary>Reverse-domain unique identifier (e.g. com.github.tgiachi.arrr.plugins.rss).</summary>
    string Id { get; }

    /// <summary>Display name of the plugin.</summary>
    string Name { get; }

    /// <summary>Semantic version string (e.g. "1.0.0").</summary>
    string Version { get; }

    /// <summary>Author name or organization.</summary>
    string Author { get; }

    /// <summary>Short description of what this plugin does.</summary>
    string Description { get; }

    /// <summary>Category tags (e.g. ["social", "messaging"]).</summary>
    string[] Categories { get; }

    /// <summary>Icon identifier or path for UI display.</summary>
    string Icon { get; }

    /// <summary>
    /// Starts the plugin. The plugin publishes events via <paramref name="context" />.EventBus
    /// until cancellation is requested.
    /// </summary>
    Task StartAsync(IPluginContext context, CancellationToken ct);
}
