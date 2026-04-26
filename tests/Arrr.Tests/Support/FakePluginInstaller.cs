using Arrr.Service.Internal;

namespace Arrr.Tests.Support;

internal class FakePluginInstaller : IPluginInstaller
{
    public Task InstallAsync(string packageId, string? version, CancellationToken ct)
        => Task.CompletedTask;

    public Task UninstallAsync(string packageId, CancellationToken ct)
        => Task.CompletedTask;

    public Task UpdateAsync(string packageId, CancellationToken ct)
        => Task.CompletedTask;
}
