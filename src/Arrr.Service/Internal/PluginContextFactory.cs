using System.Reflection;
using System.Text.Json;
using Arrr.Core.Attributes;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Arrr.Service.Internal;

internal class PluginContextFactory
{
    private readonly IEventBus _eventBus;
    private readonly DirectoriesConfig _directoriesConfig;

    public PluginContextFactory(IEventBus eventBus, DirectoriesConfig directoriesConfig)
    {
        _eventBus = eventBus;
        _directoriesConfig = directoriesConfig;
    }

    public IPluginContext Create(ISourcePlugin plugin)
    {
        var configPath = Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{plugin.Id}.config");
        var logPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "plugins", $"{plugin.Id}.log");
        var callbackUrl = $"/callback/{plugin.Name}";

        var serilogLogger = new LoggerConfiguration()
                            .MinimumLevel
                            .Debug()
                            .WriteTo
                            .File(logPath, rollingInterval: RollingInterval.Day)
                            .WriteTo
                            .Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                            .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(plugin.Id);

        return new PluginContext(configPath, logger, callbackUrl, _eventBus);
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

    public PluginContext(string configPath, ILogger logger, string callbackUrl, IEventBus eventBus)
    {
        ConfigPath = configPath;
        Logger = logger;
        CallbackUrl = callbackUrl;
        EventBus = eventBus;
    }

    public async Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (!File.Exists(ConfigPath))
        {
            Logger.LogWarning("Config not found at {Path}, using defaults", ConfigPath);
            return new T();
        }

        await using var stream = File.OpenRead(ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct) ?? new T();

        ApplySensitiveFields(config, decrypt: true);
        return config;
    }

    public async Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
    {
        ApplySensitiveFields(config!, decrypt: false);

        var dir = Path.GetDirectoryName(ConfigPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, ct);
    }

    private static void ApplySensitiveFields(object config, bool decrypt)
    {
        var props = config.GetType()
                          .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                          .Where(p => p.PropertyType == typeof(string)
                                   && p.CanRead && p.CanWrite
                                   && p.GetCustomAttribute<SensitiveAttribute>() is not null);

        foreach (var prop in props)
        {
            var value = prop.GetValue(config) as string;
            if (string.IsNullOrEmpty(value)) continue;

            prop.SetValue(config, decrypt
                ? EncryptionUtils.Decrypt(value)
                : EncryptionUtils.IsEncrypted(value) ? value : EncryptionUtils.Encrypt(value));
        }
    }
}
