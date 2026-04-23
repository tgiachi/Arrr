using System.Threading.Channels;
using Arrr.Core.Data;
using Arrr.Core.Data.Notifications;
using Arrr.Service.Internal;

namespace Arrr.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _pluginsPath;
    private readonly string _socketPath;

    public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _pluginsPath = configuration["Arrr:PluginsPath"] ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        _socketPath = configuration["Arrr:SocketPath"] ?? "/tmp/arrr.sock";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateUnbounded<Notification>(
            new()
            {
                SingleReader = true
            }
        );

        var loader = new PluginLoader(_loggerFactory.CreateLogger<PluginLoader>(), _pluginsPath);
        var plugins = loader.Load();

        _logger.LogInformation("Loaded {Count} plugin(s)", plugins.Count);

        var runner = new PluginRunner(_loggerFactory.CreateLogger<PluginRunner>(), plugins, channel.Writer);
        runner.StartAll(stoppingToken);

        await using var server = new UnixSocketServer(
            _loggerFactory.CreateLogger<UnixSocketServer>(),
            _socketPath,
            channel.Reader
        );
        await server.RunAsync(stoppingToken);
    }
}
