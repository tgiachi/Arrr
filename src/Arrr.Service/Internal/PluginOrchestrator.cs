using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Internal;

internal class PluginOrchestrator : BackgroundService
{
    private readonly ILogger _logger = Log.ForContext<PluginOrchestrator>();
    private readonly PluginContextFactory _contextFactory;
    private readonly IPluginRegistry _registry;
    private readonly ArrrConfig _config;
    private readonly string _pluginsPath;

    private readonly Dictionary<string, PluginHost> _hosts = new();
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
        _config = configService.Config;
        _pluginsPath = directoriesConfig[DirectoryType.Plugins];
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadAllPluginsAsync(stoppingToken);
        StartWatcher(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await StopAllPluginsAsync();
    }

    private async Task LoadAllPluginsAsync(CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(_pluginsPath, "*.dll"))
            await TryLoadPluginAsync(file, ct);
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

            var entry = _config.Plugins.FirstOrDefault(p => p.Id == plugin.Id);
            if (entry is null || !entry.Enabled)
            {
                _logger.Debug("Plugin {Id} not in config or disabled, skipping", plugin.Id);
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

    private async Task StartPluginAsync(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationToken ct)
    {
        if (_hosts.TryGetValue(plugin.Id, out var existing))
            await existing.StopAsync();

        var pluginCtx = _contextFactory.Create(plugin);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runTask = Task.Run(async () =>
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
        }, cts.Token);

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

    private async Task UnloadPluginByPathAsync(string dllPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(dllPath);
        var host = _hosts.Values.FirstOrDefault(h => h.PluginId.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (host is null) return;

        await host.StopAsync();
        _hosts.Remove(host.PluginId);
        _registry.Unregister(host.PluginId);
        _logger.Information("Unloaded plugin: {Id}", host.PluginId);
    }

    private async Task StopAllPluginsAsync()
    {
        foreach (var host in _hosts.Values)
            await host.StopAsync();
        _hosts.Clear();
    }
}
