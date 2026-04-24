using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakeSinkManager : ISinkManager
{
    private readonly List<AvailableSinkResponse> _available = [];

    public void Add(AvailableSinkResponse sink) => _available.Add(sink);

    public IReadOnlyList<AvailableSinkResponse> GetAvailable() => _available;

    public Task EnableAsync(string sinkId, CancellationToken ct) => Task.CompletedTask;

    public Task DisableAsync(string sinkId) => Task.CompletedTask;

    public Task ReloadAsync(string sinkId, CancellationToken ct) => Task.CompletedTask;

    public Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default)
        => Task.FromResult<PluginConfigResponse?>(
            new PluginConfigResponse(JsonSerializer.SerializeToElement(new { }), []));

    public Task SaveSinkConfigAsync(string sinkId, JsonElement config, CancellationToken ct = default)
        => Task.CompletedTask;
}
