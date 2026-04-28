using System.Text.Json;
using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Manages sink lifecycle and exposes sink metadata for the REST API.</summary>
public interface ISinkManager
{
    Task DisableAsync(string sinkId);
    Task EnableAsync(string sinkId, CancellationToken ct);
    IReadOnlyList<AvailableSinkResponse> GetAvailable();

    /// <summary>
    /// Returns the raw PNG bytes of the icon embedded in the sink's DLL,
    /// or <c>null</c> if the sink has no embedded <c>icon.png</c> resource.
    /// </summary>
    byte[]? GetSinkIcon(string sinkId);
    Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default);
    Task ReloadAsync(string sinkId, CancellationToken ct);
    Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default);

    /// <summary>
    /// Instantiates the sink with the provided <paramref name="config" /> (without persisting it)
    /// and calls <see cref="ITestableSink.TestAsync" />.
    /// Returns <c>null</c> if the sink does not implement <see cref="ITestableSink" />.
    /// Throws <see cref="KeyNotFoundException" /> if the sink is not found.
    /// </summary>
    Task<PluginTestResult?> TestSinkAsync(string sinkId, JsonElement config, CancellationToken ct = default);
}
