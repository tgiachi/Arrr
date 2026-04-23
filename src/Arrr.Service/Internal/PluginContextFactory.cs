using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

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
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(plugin.Id);

        return new PluginContext(configPath, logger, callbackUrl, _eventBus);
    }
}

internal sealed class PluginContext : IPluginContext
{
    public string ConfigPath { get; }
    public Microsoft.Extensions.Logging.ILogger Logger { get; }
    public string CallbackUrl { get; }
    public IEventBus EventBus { get; }

    public PluginContext(string configPath, Microsoft.Extensions.Logging.ILogger logger, string callbackUrl, IEventBus eventBus)
    {
        ConfigPath = configPath;
        Logger = logger;
        CallbackUrl = callbackUrl;
        EventBus = eventBus;
    }
}
