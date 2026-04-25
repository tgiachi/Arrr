using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakePluginManager : IPluginManager
{
    private readonly List<AvailablePluginResponse> _available = [];

    public void Add(AvailablePluginResponse plugin)
        => _available.Add(plugin);

    public Task DeliverCallbackAsync(string pluginId, string body, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DisableAsync(string pluginId)
        => Task.CompletedTask;

    public Task EnableAsync(string pluginId, CancellationToken ct)
        => Task.CompletedTask;

    public IReadOnlyList<AvailablePluginResponse> GetAvailable()
        => _available;

    public string? GetPendingQrCode(string pluginId)
        => null;

    public Task<PluginConfigResponse?> GetPluginConfigAsync(string pluginId, CancellationToken ct = default)
        => Task.FromResult<PluginConfigResponse?>(new PluginConfigResponse(JsonSerializer.SerializeToElement(new { }), []));

    public Task ReloadAllAsync(CancellationToken ct)
        => Task.CompletedTask;

    public Task ReloadAsync(string pluginId, CancellationToken ct)
        => Task.CompletedTask;

    public Task SavePluginConfigAsync(string pluginId, JsonElement config, CancellationToken ct = default)
        => Task.CompletedTask;
}
