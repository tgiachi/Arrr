using System.Text.Json;
using Arrr.Tray.Models;

namespace Arrr.Tray.Services;

public class SettingsService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "arrr-tray");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        if (!File.Exists(ConfigFile))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(ConfigFile);

            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
