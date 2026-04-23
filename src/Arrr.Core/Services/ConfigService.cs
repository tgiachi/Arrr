using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Microsoft.Extensions.Logging;

namespace Arrr.Core.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly string _configPath;

    public ArrrConfig Config { get; private set; } = new();

    public ConfigService(ILogger<ConfigService> logger, DirectoriesConfig directoriesConfig)
    {
        _logger = logger;
        _configPath = Path.Combine(directoriesConfig.Root, "arrr.config");
    }

    public Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Config file not found, creating default at {Path}", _configPath);
            Config = new ArrrConfig();
            JsonUtils.SerializeToFile(Config, _configPath, ArrrConfigJsonContext.Default);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Loading config from {Path}", _configPath);
        Config = JsonUtils.DeserializeFromFile<ArrrConfig>(_configPath, ArrrConfigJsonContext.Default);
        return Task.CompletedTask;
    }
}
