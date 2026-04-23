using System.Threading.Channels;
using Arrr.Core.Data;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Internal;

internal class PluginRunner
{
    private readonly ILogger<PluginRunner> _logger;
    private readonly IReadOnlyList<ISourcePlugin> _plugins;
    private readonly ChannelWriter<Notification> _writer;

    public PluginRunner(
        ILogger<PluginRunner> logger,
        IReadOnlyList<ISourcePlugin> plugins,
        ChannelWriter<Notification> writer
    )
    {
        _logger = logger;
        _plugins = plugins;
        _writer = writer;
    }

    public void StartAll(CancellationToken ct)
    {
        foreach (var plugin in _plugins)
        {
            var captured = plugin;
            _ = Task.Run(async () => await RunPluginAsync(captured, ct), ct);
        }
    }

    private async Task RunPluginAsync(ISourcePlugin plugin, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting plugin: {Name}", plugin.Name);
            await plugin.StartAsync(_writer, ct);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {Name} crashed", plugin.Name);
        }
    }
}
