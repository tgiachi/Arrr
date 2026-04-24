using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakePluginManager : IPluginManager
{
    private readonly List<AvailablePluginResponse> _available = [];

    public void Add(AvailablePluginResponse plugin) => _available.Add(plugin);

    public IReadOnlyList<AvailablePluginResponse> GetAvailable() => _available;

    public Task EnableAsync(string pluginId, CancellationToken ct) => Task.CompletedTask;

    public Task DisableAsync(string pluginId) => Task.CompletedTask;

    public Task ReloadAsync(string pluginId, CancellationToken ct) => Task.CompletedTask;

    public Task ReloadAllAsync(CancellationToken ct) => Task.CompletedTask;
}
