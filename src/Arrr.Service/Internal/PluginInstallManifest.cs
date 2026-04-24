using System.Text.Json;
using Arrr.Service.Data.Internal;

namespace Arrr.Service.Internal;

internal class PluginInstallManifest
{
    private readonly string _path;
    private List<InstalledPluginEntry> _entries = [];

    public PluginInstallManifest(string pluginsPath)
    {
        _path = Path.Combine(pluginsPath, ".manifest.json");
        Load();
    }

    public IReadOnlyList<InstalledPluginEntry> Entries => _entries;

    public void Add(InstalledPluginEntry entry)
    {
        _entries.RemoveAll(e => e.PackageId.Equals(entry.PackageId, StringComparison.OrdinalIgnoreCase));
        _entries.Add(entry);
        Save();
    }

    public InstalledPluginEntry? Remove(string packageId)
    {
        var entry = _entries.FirstOrDefault(e => e.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        _entries.Remove(entry);
        Save();
        return entry;
    }

    public InstalledPluginEntry? FindByFile(string fileName) =>
        _entries.FirstOrDefault(e => e.Files.Contains(fileName, StringComparer.OrdinalIgnoreCase));

    private void Load()
    {
        if (!File.Exists(_path)) return;
        var json = File.ReadAllText(_path);
        _entries = JsonSerializer.Deserialize<List<InstalledPluginEntry>>(json) ?? [];
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
