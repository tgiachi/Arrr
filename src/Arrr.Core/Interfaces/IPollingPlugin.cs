namespace Arrr.Core.Interfaces;

/// <summary>
/// Plugin that fetches data on a fixed interval. The service manages the polling loop;
/// the plugin only implements the fetch logic in PollAsync.
/// </summary>
public interface IPollingPlugin : ISourcePlugin
{
    /// <summary>How often PollAsync is called.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Called once per interval. Publish notifications via context.EventBus.
    /// Exceptions are caught and logged by the host — the loop keeps running.
    /// </summary>
    Task PollAsync(IPluginContext context, CancellationToken ct);
}
