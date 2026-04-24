using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Serilog;

namespace Arrr.Core.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger _logger = Log.ForContext<ConfigService>();
    private readonly string _configPath;

    public ArrrConfig Config { get; private set; } = new();

    public ConfigService(DirectoriesConfig directoriesConfig)
    {
        _configPath = Path.Combine(directoriesConfig.Root, "arrr.config");
    }

    public Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
        {
            _logger.Information("Config file not found, creating default at {Path}", _configPath);
            Config = new();
            JsonUtils.SerializeToFile(Config, _configPath, ArrrConfigJsonContext.Default);

            return Task.CompletedTask;
        }

        _logger.Information("Loading config from {Path}", _configPath);
        Config = JsonUtils.DeserializeFromFile<ArrrConfig>(_configPath, ArrrConfigJsonContext.Default);

        return Task.CompletedTask;
    }

    public void Save()
    {
        JsonUtils.SerializeToFile(Config, _configPath, ArrrConfigJsonContext.Default);
        _logger.Information("Config saved to {Path}", _configPath);
    }
}
