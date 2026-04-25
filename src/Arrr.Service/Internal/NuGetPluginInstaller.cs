using System.Runtime.InteropServices;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Internal;

internal class NuGetPluginInstaller : IPluginInstaller
{
    private static readonly ILogger _logger = Log.ForContext<NuGetPluginInstaller>();
    private static readonly NuGetFramework _targetFramework = NuGetFramework.Parse("net10.0");

    private readonly string _pluginsPath;
    private readonly IPluginManager _pluginManager;
    private readonly PluginInstallManifest _manifest;

    public NuGetPluginInstaller(DirectoriesConfig directoriesConfig, IPluginManager pluginManager)
    {
        _pluginsPath = directoriesConfig[DirectoryType.Plugins];
        _pluginManager = pluginManager;
        _manifest = new(_pluginsPath);
    }

    public async Task InstallAsync(string packageId, string? version, CancellationToken ct)
    {
        var source = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        using var cache = new SourceCacheContext();
        var resource = await source.GetResourceAsync<FindPackageByIdResource>(ct);
        var metaResource = await source.GetResourceAsync<PackageMetadataResource>(ct);

        NuGetVersion pkgVersion;

        if (version is not null)
        {
            pkgVersion = NuGetVersion.Parse(version);
        }
        else
        {
            var versions = (await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, ct)).ToList();
            pkgVersion = versions.Count > 0
                             ? versions.Max()!
                             : throw new InvalidOperationException($"Package '{packageId}' not found on NuGet.org");
        }

        // Verify the package exists via flat container (more reliable than registration during indexing)
        var knownVersions = (await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, ct)).ToList();

        if (!knownVersions.Contains(pkgVersion))
        {
            throw new InvalidOperationException($"Package '{packageId}' v{pkgVersion} not found on NuGet.org");
        }

        // Tag check via registration endpoint — may be null if NuGet indexing is still in progress.
        // The UI already pre-filters by arrr-plugin/arrr-sink tags, so a transient null is safe to skip.
        var identity = new PackageIdentity(packageId, pkgVersion);
        var metadata = await metaResource.GetMetadataAsync(identity, cache, NullLogger.Instance, ct);

        if (metadata is not null)
        {
            var tags = metadata.Tags?.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries) ?? [];
            var isPlugin = tags.Contains("arrr-plugin", StringComparer.OrdinalIgnoreCase);
            var isSink = tags.Contains("arrr-sink", StringComparer.OrdinalIgnoreCase);

            if (!isPlugin && !isSink)
            {
                throw new InvalidOperationException(
                    $"Package '{packageId}' is not an Arrr plugin or sink (missing 'arrr-plugin' / 'arrr-sink' tag)"
                );
            }
        }
        else
        {
            _logger.Warning(
                "Metadata for {PackageId} v{Version} not yet indexed — skipping tag check",
                packageId,
                pkgVersion
            );
        }

        _logger.Information("Installing {PackageId} v{Version}...", packageId, pkgVersion);

        var installedFiles = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await DownloadPackageAsync(resource, cache, packageId, pkgVersion, installedFiles, visited, ct);

        _manifest.Add(new(packageId, pkgVersion.ToString(), installedFiles.ToArray()));

        _logger.Information(
            "Installed {PackageId} v{Version} — {Count} file(s)",
            packageId,
            pkgVersion,
            installedFiles.Count
        );

        await _pluginManager.ReloadAllAsync(ct);
    }

    public async Task UninstallAsync(string packageId, CancellationToken ct)
    {
        var entry = _manifest.Remove(packageId);

        if (entry is null)
        {
            _logger.Warning("Package '{PackageId}' not found in manifest", packageId);

            return;
        }

        await _pluginManager.ReloadAllAsync(ct);

        foreach (var file in entry.Files)
        {
            var path = Path.Combine(_pluginsPath, file);

            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.Information("Deleted {File}", file);
            }
        }

        _logger.Information("Uninstalled {PackageId}", packageId);
    }

    private async Task DownloadPackageAsync(
        FindPackageByIdResource resource,
        SourceCacheContext cache,
        string packageId,
        NuGetVersion version,
        List<string> installedFiles,
        HashSet<string> visited,
        CancellationToken ct
    )
    {
        if (!visited.Add($"{packageId}:{version}"))
        {
            return;
        }

        if (IsHostProvided(packageId))
        {
            return;
        }

        using var ms = new MemoryStream();

        if (!await resource.CopyNupkgToStreamAsync(packageId, version, ms, cache, NullLogger.Instance, ct))
        {
            _logger.Warning("Could not download {PackageId} v{Version}", packageId, version);

            return;
        }

        ms.Position = 0;
        using var reader = new PackageArchiveReader(ms);

        var libItems = (await reader.GetLibItemsAsync(ct)).ToList();
        var bestGroup = GetBestGroup(libItems);

        if (bestGroup is not null)
        {
            foreach (var item in bestGroup.Items.Where(i => i.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var fileName = Path.GetFileName(item);
                var dest = Path.Combine(_pluginsPath, fileName);

                await using var src = reader.GetStream(item);
                await using var dst = File.Create(dest);
                await src.CopyToAsync(dst, ct);

                if (!installedFiles.Contains(fileName))
                {
                    installedFiles.Add(fileName);
                }
            }
        }

        // Extract native tool binaries matching the current runtime (e.g. whatsapp-bridge)
        var toolFiles = reader.GetFiles(GetNativeToolsPath()).ToList();

        foreach (var item in toolFiles)
        {
            var fileName = Path.GetFileName(item);
            var dest = Path.Combine(_pluginsPath, fileName);

            await using var src = reader.GetStream(item);
            await using var dst = File.Create(dest);
            await src.CopyToAsync(dst, ct);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    dest,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute
                );
            }

            if (!installedFiles.Contains(fileName))
            {
                installedFiles.Add(fileName);
            }
        }

        var nuspec = await reader.GetNuspecReaderAsync(ct);

        foreach (var dep in nuspec.GetDependencyGroups().SelectMany(g => g.Packages))
        {
            var depVersions = await resource.GetAllVersionsAsync(dep.Id, cache, NullLogger.Instance, ct);
            var best = depVersions.FindBestMatch(dep.VersionRange, v => v);

            if (best is not null)
            {
                await DownloadPackageAsync(resource, cache, dep.Id, best, installedFiles, visited, ct);
            }
        }
    }

    private static FrameworkSpecificGroup? GetBestGroup(IList<FrameworkSpecificGroup> groups)
    {
        var reducer = new FrameworkReducer();
        var best = reducer.GetNearest(_targetFramework, groups.Select(g => g.TargetFramework));

        return best is null ? null : groups.FirstOrDefault(g => g.TargetFramework == best);
    }

    private static string GetNativeToolsPath()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _                  => "x64"
        };

        return $"tools/{os}-{arch}";
    }

    private static bool IsHostProvided(string id)
        => id.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
           id.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
           id.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase) ||
           id.Equals("Arrr.Core", StringComparison.OrdinalIgnoreCase) ||
           id.Equals("NETStandard.Library", StringComparison.OrdinalIgnoreCase);
}
