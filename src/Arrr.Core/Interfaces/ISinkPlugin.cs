using Arrr.Core.Data.Notifications;

namespace Arrr.Core.Interfaces;

/// <summary>Contract for notification sink plugins — output connectors that deliver notifications.</summary>
public interface ISinkPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }
    string Icon { get; }

    /// <summary>
    /// Supported OS platforms. Empty array means compatible with all platforms.
    /// Use OS names from <see cref="System.Runtime.InteropServices.OSPlatform"/>: "Linux", "Windows", "OSX".
    /// </summary>
    string[] Platforms => [];

    Task ConsumeAsync(Notification notification, CancellationToken ct);

    Task StartAsync(ISinkContext context, CancellationToken ct);
    Task StopAsync();
}
