using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginHost
{
    private readonly ISourcePlugin _plugin;
    private readonly PluginLoadContext _loadContext;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;

    public ISourcePlugin Plugin => _plugin;
    public string PluginId => _plugin.Id;

    public PluginHost(ISourcePlugin plugin, PluginLoadContext loadContext, CancellationTokenSource cts, Task runTask)
    {
        _plugin = plugin;
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
        _loadContext.Unload();
    }
}
