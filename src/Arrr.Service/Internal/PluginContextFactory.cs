using System.Text.Json;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Arrr.Service.Internal;

internal class PluginContextFactory : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly SocketsHttpHandler _httpHandler;

    public PluginContextFactory(IEventBus eventBus, DirectoriesConfig directoriesConfig)
    {
        _eventBus = eventBus;
        _directoriesConfig = directoriesConfig;
        _httpHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
    }

    public IPluginContext Create(ISourcePlugin plugin)
    {
        var configPath = GetConfigPath(plugin.Id);
        var logPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "plugins", $"{plugin.Id}.log");
        var callbackUrl = $"/callback/{plugin.Name}";

        var serilogLogger = new LoggerConfiguration()
                            .MinimumLevel
                            .Debug()
                            .WriteTo
                            .File(logPath, rollingInterval: RollingInterval.Day)
                            .WriteTo
                            .Console(
                                outputTemplate:
                                "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                            )
                            .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(plugin.Id);
        var http = new HttpClient(_httpHandler, disposeHandler: false);

        return new PluginContext(configPath, logger, callbackUrl, _eventBus, http);
    }

    public string GetConfigPath(string pluginId)
        => Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{pluginId}.config");

    public void Dispose()
    {
        _httpHandler.Dispose();
    }
}

internal sealed class PluginContext : IPluginContext
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigPath { get; }
    public ILogger Logger { get; }
    public string CallbackUrl { get; }
    public IEventBus EventBus { get; }
    public HttpClient Http { get; }

    public PluginContext(string configPath, ILogger logger, string callbackUrl, IEventBus eventBus, HttpClient http)
    {
        ConfigPath = configPath;
        Logger = logger;
        CallbackUrl = callbackUrl;
        EventBus = eventBus;
        Http = http;
    }

    public async Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (!File.Exists(ConfigPath))
        {
            Logger.LogWarning("Config not found at {Path}, using defaults", ConfigPath);

            return new();
        }

        await using var stream = File.OpenRead(ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct) ?? new T();

        EncryptionUtils.ApplySensitiveFields(config, true);

        return config;
    }

    public async Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
    {
        EncryptionUtils.ApplySensitiveFields(config!, false);

        var dir = Path.GetDirectoryName(ConfigPath);

        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, ct);
    }
}
