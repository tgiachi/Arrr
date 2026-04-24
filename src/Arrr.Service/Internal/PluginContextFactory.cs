using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
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
}
