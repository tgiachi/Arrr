using System.Text.Json;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;

namespace Arrr.Service.Internal;

internal class ConfigBackupService : IConfigBackupService
{
    private readonly DirectoriesConfig _directoriesConfig;

    public ConfigBackupService(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public async Task<Dictionary<string, JsonElement>> ExportAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, JsonElement>();
        var configsDir = _directoriesConfig[DirectoryType.Configs];

        if (!Directory.Exists(configsDir))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(configsDir, "*.config"))
        {
            var key = Path.GetFileNameWithoutExtension(file);
            var json = await File.ReadAllTextAsync(file, ct);
            using var doc = JsonDocument.Parse(json);
            result[key] = doc.RootElement.Clone();
        }

        return result;
    }

    public async Task<int> ImportAsync(Dictionary<string, JsonElement> configs, CancellationToken ct)
    {
        var configsDir = _directoriesConfig[DirectoryType.Configs];
        Directory.CreateDirectory(configsDir);

        foreach (var (key, value) in configs)
        {
            var path = Path.Combine(configsDir, $"{key}.config");
            await File.WriteAllTextAsync(path, value.GetRawText(), ct);
        }

        return configs.Count;
    }
}
