using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Optional interface for plugins that can validate their configuration
/// (e.g. reachability check, credential verification).
/// </summary>
public interface ITestablePlugin
{
    /// <summary>
    /// Runs a connectivity / credential check using the config already loaded via
    /// <see cref="IPluginContext.LoadConfigAsync{T}" /> during <c>StartAsync</c>.
    /// Must not publish events or modify persistent state.
    /// </summary>
    Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct);
}
