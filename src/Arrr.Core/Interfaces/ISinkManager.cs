using System.Text.Json;
using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Manages sink lifecycle and exposes sink metadata for the REST API.</summary>
public interface ISinkManager
{
    IReadOnlyList<AvailableSinkResponse> GetAvailable();
    Task EnableAsync(string sinkId, CancellationToken ct);
    Task DisableAsync(string sinkId);
    Task ReloadAsync(string sinkId, CancellationToken ct);
    Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default);
    Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default);
}
