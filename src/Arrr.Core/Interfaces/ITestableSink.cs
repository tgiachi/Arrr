using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Optional interface for sinks that can validate their configuration
/// (e.g. reachability check, credential verification).
/// </summary>
public interface ITestableSink
{
    /// <summary>
    /// Runs a connectivity / credential check using the config already loaded via
    /// <see cref="ISinkContext.LoadConfigAsync{T}" /> during <c>StartAsync</c>.
    /// Must not deliver actual messages or modify persistent state.
    /// </summary>
    Task<PluginTestResult> TestAsync(ISinkContext context, CancellationToken ct);
}
