using System.Threading.Channels;
using Arrr.Core.Data;
using Arrr.Core.Data.Notifications;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Contract for notification source plugins.
/// Each plugin connects to an external source and pushes notifications into the aggregator pipeline.
/// </summary>
public interface ISourcePlugin
{
    /// <summary>The unique name of this source (e.g. "rss", "imap").</summary>
    string Name { get; }

    /// <summary>Icon identifier or path for UI display.</summary>
    string Icon { get; }

    /// <summary>
    /// Starts the plugin. The plugin writes notifications to <paramref name="writer" /> until
    /// cancellation is requested.
    /// </summary>
    Task StartAsync(ChannelWriter<Notification> writer, CancellationToken ct);
}
