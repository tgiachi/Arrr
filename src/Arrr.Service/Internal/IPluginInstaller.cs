namespace Arrr.Service.Internal;

internal interface IPluginInstaller
{
    Task InstallAsync(string packageId, string? version, CancellationToken ct);
    Task UninstallAsync(string packageId, CancellationToken ct);
}
