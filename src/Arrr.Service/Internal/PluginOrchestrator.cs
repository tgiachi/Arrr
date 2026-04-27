using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Arrr.Core.Attributes;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Internal;

internal class PluginOrchestrator : BackgroundService, IPluginManager
{
    private readonly ILogger _logger = Log.ForContext<PluginOrchestrator>();
    private readonly PluginContextFactory _contextFactory;
    private readonly IPluginRegistry _registry;
    private readonly IConfigService _configService;
    private readonly ArrrConfig _config;
    private readonly string _pluginsPath;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, PluginHost> _hosts = new();
    private readonly Dictionary<string, string> _dllPaths = new();
    private FileSystemWatcher? _watcher;

    public PluginOrchestrator(
        PluginContextFactory contextFactory,
        IPluginRegistry registry,
        IConfigService configService,
        DirectoriesConfig directoriesConfig
    )
    {
        _contextFactory = contextFactory;
        _registry = registry;
        _configService = configService;
        _config = configService.Config;
        _pluginsPath = directoriesConfig[DirectoryType.Plugins];
    }

    public async Task DeliverCallbackAsync(string pluginId, string body, CancellationToken ct = default)
    {
        if (!_hosts.TryGetValue(pluginId, out var host))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not running.");
        }

        if (host.Plugin is not ICallbackPlugin callbackPlugin)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' does not support callbacks.");
        }

        await callbackPlugin.HandleCallbackAsync(body, ct);
    }

    public async Task DisableAsync(string pluginId)
    {
        var entry = _config.Plugins.FirstOrDefault(p => p.Id == pluginId);

        if (entry is not null)
        {
            entry.Enabled = false;
        }

        _configService.Save();

        if (_hosts.TryGetValue(pluginId, out var host))
        {
            await host.StopAsync();
            _hosts.Remove(pluginId);
            _registry.Unregister(pluginId);
            _logger.Information("Disabled plugin: {Id}", pluginId);
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }

    public async Task EnableAsync(string pluginId, CancellationToken ct)
    {
        var entry = _config.Plugins.FirstOrDefault(p => p.Id == pluginId);

        if (entry is null)
        {
            _config.Plugins.Add(new() { Id = pluginId, Enabled = true });
        }
        else
        {
            entry.Enabled = true;
        }

        _configService.Save();

        if (_dllPaths.TryGetValue(pluginId, out var dll))
        {
            await TryLoadPluginAsync(dll, ct);
        }
        else
        {
            _logger.Warning("DLL for plugin {Id} not found in cache, rescanning", pluginId);
            await LoadAllPluginsAsync(ct);
        }
    }

    public IReadOnlyList<AvailablePluginResponse> GetAvailable()
    {
        var result = new List<AvailablePluginResponse>();

        foreach (var dll in Directory.EnumerateFiles(_pluginsPath, "*.dll"))
        {
            try
            {
                var ctx = new PluginLoadContext(dll);
                var assembly = ctx.LoadFromAssemblyPath(dll);
                var pluginType = assembly.GetTypes()
                                         .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

                if (pluginType is null)
                {
                    ctx.Unload();

                    continue;
                }

                if (Activator.CreateInstance(pluginType) is not ISourcePlugin plugin)
                {
                    ctx.Unload();

                    continue;
                }

                var entry = _config.Plugins.FirstOrDefault(p => p.Id == plugin.Id);

                result.Add(
                    new(
                        plugin.Id,
                        plugin.Name,
                        plugin.Version,
                        plugin.Author,
                        plugin.Description,
                        plugin.Categories,
                        plugin.Icon,
                        entry is { Enabled: true },
                        _hosts.ContainsKey(plugin.Id),
                        plugin is ICallbackPlugin,
                        plugin is IQrPlugin,
                        plugin is ITestablePlugin
                    )
                );

                ctx.Unload();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to inspect plugin at {Path}", dll);
            }
        }

        return result;
    }

    public string? GetPendingQrCode(string pluginId)
    {
        if (!_hosts.TryGetValue(pluginId, out var host))
        {
            throw new KeyNotFoundException($"Plugin '{pluginId}' is not running.");
        }

        if (host.Plugin is not IQrPlugin qrPlugin)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' does not support QR pairing.");
        }

        return qrPlugin.PendingQrCode;
    }

    public async Task<PluginConfigResponse?> GetPluginConfigAsync(string pluginId, CancellationToken ct = default)
    {
        var dll = ResolveDllPath(pluginId);

        if (dll is null)
        {
            return null;
        }

        var ctx = new PluginLoadContext(dll);

        try
        {
            var assembly = ctx.LoadFromAssemblyPath(dll);
            var pluginType = assembly.GetTypes()
                                     .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

            if (pluginType is null || Activator.CreateInstance(pluginType) is not IConfigurablePlugin configurable)
            {
                return null;
            }

            var configType = configurable.ConfigType;
            var configPath = _contextFactory.GetConfigPath(pluginId);

            object config;

            if (File.Exists(configPath))
            {
                await using var stream = File.OpenRead(configPath);
                config = await JsonSerializer.DeserializeAsync(stream, configType, _jsonOpts, ct) ??
                         Activator.CreateInstance(configType)!;
            }
            else
            {
                config = Activator.CreateInstance(configType)!;
            }

            EncryptionUtils.ApplySensitiveFields(config, true);

            var values = JsonSerializer.SerializeToElement(config, configType, _jsonOpts);
            var schema = BuildSchema(configType);

            return new(values, schema);
        }
        finally
        {
            ctx.Unload();
        }
    }

    public async Task ReloadAllAsync(CancellationToken ct)
    {
        _logger.Information("Reloading all plugins...");
        await StopAllPluginsAsync();
        _dllPaths.Clear();
        await LoadAllPluginsAsync(ct);
    }

    public async Task ReloadAsync(string pluginId, CancellationToken ct)
    {
        if (_hosts.TryGetValue(pluginId, out var host))
        {
            await host.StopAsync();
            _hosts.Remove(pluginId);
            _registry.Unregister(pluginId);
        }

        if (_dllPaths.TryGetValue(pluginId, out var dll))
        {
            await TryLoadPluginAsync(dll, ct);
            _logger.Information("Reloaded plugin: {Id}", pluginId);
        }
        else
        {
            _logger.Warning("Cannot reload {Id}: DLL not found in cache", pluginId);
        }
    }

    public async Task SavePluginConfigAsync(string pluginId, JsonElement incoming, CancellationToken ct = default)
    {
        var dll = ResolveDllPath(pluginId) ?? throw new KeyNotFoundException($"Plugin '{pluginId}' not found.");

        var ctx = new PluginLoadContext(dll);

        try
        {
            var assembly = ctx.LoadFromAssemblyPath(dll);
            var pluginType = assembly.GetTypes()
                                     .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

            if (pluginType is null || Activator.CreateInstance(pluginType) is not IConfigurablePlugin configurable)
            {
                throw new InvalidOperationException($"Plugin '{pluginId}' does not expose a config type.");
            }

            var configType = configurable.ConfigType;
            var config = incoming.Deserialize(configType, _jsonOpts) ?? Activator.CreateInstance(configType)!;

            EncryptionUtils.ApplySensitiveFields(config, false);

            var configPath = _contextFactory.GetConfigPath(pluginId);
            var dir = Path.GetDirectoryName(configPath);

            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            await using var stream = File.Create(configPath);
            await JsonSerializer.SerializeAsync(stream, config, configType, _jsonOpts, ct);
        }
        finally
        {
            ctx.Unload();
        }
    }

    public async Task<PluginTestResult?> TestPluginAsync(
        string pluginId,
        JsonElement config,
        CancellationToken ct = default)
    {
        var dll = ResolveDllPath(pluginId) ?? throw new KeyNotFoundException($"Plugin '{pluginId}' not found.");
        var ctx = new PluginLoadContext(dll);

        try
        {
            var assembly = ctx.LoadFromAssemblyPath(dll);
            var pluginType = assembly.GetTypes()
                                     .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

            if (pluginType is null || Activator.CreateInstance(pluginType) is not ISourcePlugin plugin)
            {
                return null;
            }

            if (plugin is not ITestablePlugin testable)
            {
                return null;
            }

            using var testCtx = new TestPluginContext(config);
            await plugin.StartAsync(testCtx, ct);
            return await testable.TestAsync(testCtx, ct);
        }
        finally
        {
            ctx.Unload();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadAllPluginsAsync(stoppingToken);
        StartWatcher(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await StopAllPluginsAsync();
    }

    private static ConfigFieldInfo[] BuildSchema(Type configType)
        => configType
           .GetProperties(BindingFlags.Public | BindingFlags.Instance)
           .Select(
               p => new ConfigFieldInfo(
                   p.Name,
                   p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                   p.GetCustomAttribute<SensitiveAttribute>() is not null
               )
           )
           .ToArray();

    private async Task LoadAllPluginsAsync(CancellationToken ct)
    {
        var dlls = Directory.EnumerateFiles(_pluginsPath, "*.dll").ToList();
        _logger.Information("Scanning plugins directory: {Path} ({Count} DLL(s) found)", _pluginsPath, dlls.Count);

        foreach (var file in dlls)
        {
            await TryLoadPluginAsync(file, ct);
        }

        _logger.Information(
            "Plugin startup complete — {Running}/{Total} plugin(s) running",
            _hosts.Count,
            dlls.Count
        );
    }

    private string? ResolveDllPath(string pluginId)
    {
        if (_dllPaths.TryGetValue(pluginId, out var cached))
        {
            return cached;
        }

        foreach (var dll in Directory.EnumerateFiles(_pluginsPath, "*.dll"))
        {
            try
            {
                var ctx = new PluginLoadContext(dll);

                try
                {
                    var asm = ctx.LoadFromAssemblyPath(dll);
                    var t = asm.GetTypes().FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

                    if (t is null)
                    {
                        continue;
                    }

                    if (Activator.CreateInstance(t) is not ISourcePlugin p)
                    {
                        continue;
                    }

                    if (p.Id == pluginId)
                    {
                        return dll;
                    }
                }
                finally { ctx.Unload(); }
            }
            catch
            {
                /* ignore load errors for individual DLLs */
            }
        }

        return null;
    }

    private async Task StartPluginAsync(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationToken ct)
    {
        if (_hosts.TryGetValue(plugin.Id, out var existing))
        {
            await existing.StopAsync();
        }

        var pluginCtx = _contextFactory.Create(plugin);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var runTask = plugin is IPollingPlugin pollingPlugin
                          ? Task.Run(
                              async () =>
                              {
                                  try
                                  {
                                      await plugin.StartAsync(pluginCtx, cts.Token);
                                  }
                                  catch (OperationCanceledException) { return; }
                                  catch (Exception ex)
                                  {
                                      _logger.Error(ex, "Plugin {Id} StartAsync failed", plugin.Id);

                                      return;
                                  }

                                  while (!cts.Token.IsCancellationRequested)
                                  {
                                      try
                                      {
                                          await pollingPlugin.PollAsync(pluginCtx, cts.Token);
                                      }
                                      catch (OperationCanceledException) { break; }
                                      catch (Exception ex)
                                      {
                                          _logger.Error(ex, "Plugin {Id} poll error", plugin.Id);
                                      }

                                      await Task.Delay(pollingPlugin.Interval, cts.Token)
                                                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                                  }
                              },
                              cts.Token
                          )
                          : Task.Run(
                              async () =>
                              {
                                  try
                                  {
                                      await plugin.StartAsync(pluginCtx, cts.Token);
                                  }
                                  catch (OperationCanceledException) { }
                                  catch (Exception ex)
                                  {
                                      _logger.Error(ex, "Plugin {Id} crashed", plugin.Id);
                                  }
                              },
                              cts.Token
                          );

        var host = new PluginHost(plugin, loadContext, cts, runTask);
        _hosts[plugin.Id] = host;
        _registry.Register(plugin);

        _logger.Information("Started plugin: {Id} v{Version}", plugin.Id, plugin.Version);
    }

    private void StartWatcher(CancellationToken ct)
    {
        _watcher = new(_pluginsPath, "*.dll")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) => _ = TryLoadPluginAsync(e.FullPath, ct);
        _watcher.Changed += (_, e) => _ = TryLoadPluginAsync(e.FullPath, ct);
        _watcher.Deleted += (_, e) => _ = UnloadPluginByPathAsync(e.FullPath);
    }

    private async Task StopAllPluginsAsync()
    {
        foreach (var host in _hosts.Values)
        {
            await host.StopAsync();
        }
        _hosts.Clear();
    }

    private async Task TryLoadPluginAsync(string dllPath, CancellationToken ct)
    {
        try
        {
            var context = new PluginLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);
            var pluginType = assembly.GetTypes()
                                     .FirstOrDefault(t => !t.IsAbstract && t.IsAssignableTo(typeof(ISourcePlugin)));

            if (pluginType is null)
            {
                context.Unload();

                return;
            }

            if (Activator.CreateInstance(pluginType) is not ISourcePlugin plugin)
            {
                context.Unload();

                return;
            }

            if (!PlatformUtils.IsCompatible(plugin.Platforms))
            {
                _logger.Warning(
                    "Plugin {Id} requires platform(s) [{Platforms}], skipping on current OS",
                    plugin.Id,
                    string.Join(", ", plugin.Platforms)
                );
                context.Unload();

                return;
            }

            _dllPaths[plugin.Id] = dllPath;

            var entry = _config.Plugins.FirstOrDefault(p => p.Id == plugin.Id);

            if (entry is null || !entry.Enabled)
            {
                _logger.Information("Plugin {Id} not in config or disabled, skipping", plugin.Id);
                context.Unload();

                return;
            }

            await StartPluginAsync(plugin, context, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load plugin from {Path}", dllPath);
        }
    }

    private async Task UnloadPluginByPathAsync(string dllPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(dllPath);
        var host = _hosts.Values.FirstOrDefault(h => h.PluginId.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (host is null)
        {
            return;
        }

        await host.StopAsync();
        _hosts.Remove(host.PluginId);
        _registry.Unregister(host.PluginId);
        _logger.Information("Unloaded plugin: {Id}", host.PluginId);
    }
}
