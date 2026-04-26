using System.Text.Json;
using Arrr.Core.Data.Api;

namespace Arrr.Core.Interfaces;

/// <summary>Manages sink lifecycle and exposes sink metadata for the REST API.</summary>
public interface ISinkManager
{
    Task DisableAsync(string sinkId);
    Task EnableAsync(string sinkId, CancellationToken ct);
    IReadOnlyList<AvailableSinkResponse> GetAvailable();
    Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default);
    Task ReloadAsync(string sinkId, CancellationToken ct);
    Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default);
}
