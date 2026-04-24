using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Service.Sinks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Arrr.Service.Internal;

internal class SinkOrchestrator : BackgroundService, ISinkManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Serilog.ILogger _logger = Log.ForContext<SinkOrchestrator>();
    private readonly IEventBus _eventBus;
    private readonly IConfigService? _configService;
    private readonly DirectoriesConfig? _directoriesConfig;

    private readonly ISinkPlugin[] _builtIns;
    private readonly Dictionary<string, ISinkPlugin> _running = [];
    private readonly List<(ISinkPlugin Sink, bool Enabled)> _testEntries = [];

    internal SinkOrchestrator(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _builtIns = [];
    }

    public SinkOrchestrator(IEventBus eventBus, IConfigService configService, DirectoriesConfig directoriesConfig)
    {
        _eventBus = eventBus;
        _configService = configService;
        _directoriesConfig = directoriesConfig;
        _builtIns = [new DbusNotifySink(), new UnixSocketSink()];
    }

    internal void AddSinkForTest(ISinkPlugin sink, bool enabled = true)
        => _testEntries.Add((sink, enabled));

    internal async Task StartSinksForTestAsync(CancellationToken ct)
    {
        foreach (var (sink, enabled) in _testEntries)
        {
            if (!enabled)
                continue;
            await sink.StartAsync(new SinkContext("", NullLogger.Instance), ct);
            _running[sink.Id] = sink;
        }
        SubscribeToNotifications();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartBuiltInSinksAsync(stoppingToken);
        await LoadExternalSinksAsync(stoppingToken);
        SubscribeToNotifications();

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await StopAllAsync();
    }

    private void SubscribeToNotifications()
    {
        _eventBus.Subscribe<Notification>(async (notification, ct) =>
        {
            foreach (var sink in _running.Values.ToList())
            {
                try
                {
                    await sink.ConsumeAsync(notification, ct);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Sink {Id} error consuming notification", sink.Id);
                }
            }
        });
    }

    private async Task StartBuiltInSinksAsync(CancellationToken ct)
    {
        var config = _configService!.Config;
        foreach (var sink in _builtIns)
        {
            var entry = config.Sinks.FirstOrDefault(s => s.Id == sink.Id);
            if (entry is { Enabled: false })
                continue;

            try
            {
                await sink.StartAsync(CreateContext(sink.Id), ct);
                _running[sink.Id] = sink;
                _logger.Information("Started built-in sink: {Id}", sink.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start built-in sink: {Id}", sink.Id);
            }
        }
    }

    private async Task LoadExternalSinksAsync(CancellationToken ct)
    {
        var pluginsPath = _directoriesConfig![DirectoryType.Plugins];
        var config = _configService!.Config;

        foreach (var dll in Directory.EnumerateFiles(pluginsPath, "*.dll"))
        {
            try
            {
                var loadCtx = new PluginLoadContext(dll);
                var asm = loadCtx.LoadFromAssemblyPath(dll);
                var sinkType = asm.GetTypes()
                                  .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISinkPlugin)));

                if (sinkType is null) { loadCtx.Unload(); continue; }
                if (Activator.CreateInstance(sinkType) is not ISinkPlugin sink) { loadCtx.Unload(); continue; }

                var entry = config.Sinks.FirstOrDefault(s => s.Id == sink.Id);
                if (entry is null || !entry.Enabled) { loadCtx.Unload(); continue; }

                await sink.StartAsync(CreateContext(sink.Id), ct);
                _running[sink.Id] = sink;
                _logger.Information("Started external sink: {Id}", sink.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load external sink from {Path}", dll);
            }
        }
    }

    private ISinkContext CreateContext(string sinkId)
    {
        if (_directoriesConfig is null)
            return new SinkContext("", NullLogger.Instance);

        var configPath = Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{sinkId}.sink.config");
        var logPath = Path.Combine(_directoriesConfig[DirectoryType.Logs], "sinks", $"{sinkId}.log");

        var serilogLogger = new LoggerConfiguration()
                            .MinimumLevel.Debug()
                            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                            .WriteTo.Console(
                                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
                            )
                            .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(sinkId);
        return new SinkContext(configPath, logger);
    }

    private async Task StopAllAsync()
    {
        foreach (var sink in _running.Values)
        {
            try { await sink.StopAsync(); }
            catch (Exception ex) { _logger.Error(ex, "Sink {Id} stop error", sink.Id); }
        }
        _running.Clear();
    }

    public IReadOnlyList<AvailableSinkResponse> GetAvailable()
    {
        if (_configService is null)
        {
            return _testEntries.Select(e => new AvailableSinkResponse(
                e.Sink.Id, e.Sink.Name, e.Sink.Version, e.Sink.Author, e.Sink.Description, e.Sink.Icon,
                Enabled: e.Enabled,
                Running: _running.ContainsKey(e.Sink.Id),
                IsBuiltIn: false,
                HasConfig: e.Sink is IConfigurablePlugin
            )).ToList();
        }

        var result = new List<AvailableSinkResponse>();
        var config = _configService.Config;

        foreach (var sink in _builtIns)
        {
            var entry = config.Sinks.FirstOrDefault(s => s.Id == sink.Id);
            result.Add(new AvailableSinkResponse(
                sink.Id, sink.Name, sink.Version, sink.Author, sink.Description, sink.Icon,
                Enabled: entry is null || entry.Enabled,
                Running: _running.ContainsKey(sink.Id),
                IsBuiltIn: true,
                HasConfig: sink is IConfigurablePlugin
            ));
        }

        foreach (var dll in Directory.EnumerateFiles(_directoriesConfig![DirectoryType.Plugins], "*.dll"))
        {
            try
            {
                var loadCtx = new PluginLoadContext(dll);
                var asm = loadCtx.LoadFromAssemblyPath(dll);
                var sinkType = asm.GetTypes()
                                  .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISinkPlugin)));

                if (sinkType is null) { loadCtx.Unload(); continue; }
                if (Activator.CreateInstance(sinkType) is not ISinkPlugin sink) { loadCtx.Unload(); continue; }

                var entry = config.Sinks.FirstOrDefault(s => s.Id == sink.Id);
                result.Add(new AvailableSinkResponse(
                    sink.Id, sink.Name, sink.Version, sink.Author, sink.Description, sink.Icon,
                    Enabled: entry?.Enabled ?? false,
                    Running: _running.ContainsKey(sink.Id),
                    IsBuiltIn: false,
                    HasConfig: sink is IConfigurablePlugin
                ));
                loadCtx.Unload();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to inspect external sink at {Path}", dll);
            }
        }

        return result;
    }

    public async Task EnableAsync(string sinkId, CancellationToken ct)
    {
        var config = _configService!.Config;
        var entry = config.Sinks.FirstOrDefault(s => s.Id == sinkId);
        if (entry is null)
            config.Sinks.Add(new SinkEntry { Id = sinkId, Enabled = true });
        else
            entry.Enabled = true;
        _configService.Save();

        var builtIn = _builtIns.FirstOrDefault(s => s.Id == sinkId);
        if (builtIn is not null && !_running.ContainsKey(sinkId))
        {
            await builtIn.StartAsync(CreateContext(sinkId), ct);
            _running[sinkId] = builtIn;
            _logger.Information("Enabled built-in sink: {Id}", sinkId);
        }
    }

    public async Task DisableAsync(string sinkId)
    {
        var config = _configService!.Config;
        var entry = config.Sinks.FirstOrDefault(s => s.Id == sinkId);
        if (entry is null)
            config.Sinks.Add(new SinkEntry { Id = sinkId, Enabled = false });
        else
            entry.Enabled = false;
        _configService.Save();

        if (_running.TryGetValue(sinkId, out var sink))
        {
            await sink.StopAsync();
            _running.Remove(sinkId);
            _logger.Information("Disabled sink: {Id}", sinkId);
        }
    }

    public async Task ReloadAsync(string sinkId, CancellationToken ct)
    {
        if (_running.TryGetValue(sinkId, out var running))
        {
            await running.StopAsync();
            _running.Remove(sinkId);
        }

        var builtIn = _builtIns.FirstOrDefault(s => s.Id == sinkId);
        if (builtIn is not null)
        {
            await builtIn.StartAsync(CreateContext(sinkId), ct);
            _running[sinkId] = builtIn;
            _logger.Information("Reloaded sink: {Id}", sinkId);
        }
    }

    public async Task<PluginConfigResponse?> GetSinkConfigAsync(string sinkId, CancellationToken ct = default)
    {
        var sink = _builtIns.FirstOrDefault(s => s.Id == sinkId);
        if (sink is not IConfigurablePlugin configurable)
            return null;

        var configType = configurable.ConfigType;
        var configPath = _directoriesConfig is not null
            ? Path.Combine(_directoriesConfig[DirectoryType.Configs], $"{sinkId}.sink.config")
            : "";

        object config;
        if (File.Exists(configPath))
        {
            await using var stream = File.OpenRead(configPath);
            config = (await JsonSerializer.DeserializeAsync(stream, configType, JsonOpts, ct))
                     ?? Activator.CreateInstance(configType)!;
        }
        else
        {
            config = Activator.CreateInstance(configType)!;
        }

        var values = JsonSerializer.SerializeToElement(config, configType, JsonOpts);
        return new PluginConfigResponse(values, BuildSchema(configType));
    }

    public async Task SaveSinkConfigAsync(string sinkId, JsonElement incoming, CancellationToken ct = default)
    {
        var sink = _builtIns.FirstOrDefault(s => s.Id == sinkId) as IConfigurablePlugin
                   ?? throw new KeyNotFoundException($"Sink '{sinkId}' not found or not configurable.");

        var configType = sink.ConfigType;
        var config = incoming.Deserialize(configType, JsonOpts)
                     ?? Activator.CreateInstance(configType)!;

        var configPath = Path.Combine(_directoriesConfig![DirectoryType.Configs], $"{sinkId}.sink.config");
        var dir = Path.GetDirectoryName(configPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, config, configType, JsonOpts, ct);
    }

    private static ConfigFieldInfo[] BuildSchema(Type configType) =>
        configType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new ConfigFieldInfo(
                p.Name,
                p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                false
            ))
            .ToArray();
}

internal sealed class SinkContext : ISinkContext
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string ConfigPath { get; }
    public ILogger Logger { get; }

    public SinkContext(string configPath, ILogger logger)
    {
        ConfigPath = configPath;
        Logger = logger;
    }

    public async Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (!File.Exists(ConfigPath))
            return new T();

        await using var stream = File.OpenRead(ConfigPath);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOpts, ct) ?? new T();
    }

    public async Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOpts, ct);
    }
}
