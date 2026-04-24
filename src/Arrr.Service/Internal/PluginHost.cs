using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginHost
{
    private readonly PluginLoadContext _loadContext;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;

    public ISourcePlugin Plugin { get; }

    public string PluginId => Plugin.Id;

    public PluginHost(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationTokenSource cts, Task runTask)
    {
        Plugin = plugin;
        _loadContext = loadContext;
        _cts = cts;
        _runTask = runTask;
    }

    public async Task StopAsync()
    {
        await _cts.CancelAsync();

        try
        {
            await _runTask;
        }
        catch (OperationCanceledException) { }

        _cts.Dispose();

        if (Plugin is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _loadContext.Unload();
    }
}
