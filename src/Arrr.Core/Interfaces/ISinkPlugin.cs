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
    Task ConsumeAsync(Notification notification, CancellationToken ct);

    Task StartAsync(ISinkContext context, CancellationToken ct);
    Task StopAsync();
}
