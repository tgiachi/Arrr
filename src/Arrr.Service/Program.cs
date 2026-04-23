using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Extensions.Logger;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Arrr.Service.Api;
using Arrr.Service.Interfaces;
using Arrr.Service.Internal;
using Arrr.Service.Services;
using Arrr.Service.Subscribers;
using ConsoleAppFramework;
using Serilog;

JsonUtils.RegisterJsonContext(ArrrConfigJsonContext.Default);

await ConsoleApp.RunAsync(
    args,
    async (
        string? rootDirectory = null,
        LogLevelType logLevelType = LogLevelType.Information,
        bool logToFile = true,
        CancellationToken ct = default
    ) =>
    {
        rootDirectory ??= Environment.CurrentDirectory;

        var directoriesConfig = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());

        var loggerConfiguration = new LoggerConfiguration()
                                  .MinimumLevel
                                  .Is(logLevelType.ToSerilogLogLevel())
                                  .WriteTo
                                  .Console();

        if (logToFile)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.File(
                Path.Combine(directoriesConfig[DirectoryType.Logs], "log-.txt"),
                rollingInterval: RollingInterval.Day
            );
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        Log.Logger.Information("Root directory: {RootDirectory}", rootDirectory);

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton(directoriesConfig);
        builder.Services.AddSingleton<IConfigService, ConfigService>();
        builder.Services.AddSingleton<EventBusService>();
        builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBusService>());
        builder.Services.AddSingleton<IPluginRegistry, PluginRegistryService>();
        builder.Services.AddSingleton<UnixSocketServer>(
            sp => new(sp.GetRequiredService<IConfigService>().Config.SocketPath)
        );
        builder.Services.AddSingleton<SocketBroadcastSubscriber>();
        builder.Services.AddSingleton<PluginContextFactory>();
        builder.Services.AddHostedService<EventBusHostedService>();
        builder.Services.AddHostedService<PluginOrchestrator>();
        builder.Services.AddHostedService<DBusNotifySubscriber>();

        builder.Logging.ClearProviders().AddSerilog();

        var app = builder.Build();

        var configService = app.Services.GetRequiredService<IConfigService>();
        await configService.LoadAsync(ct);

        app.Urls.Add($"http://0.0.0.0:{configService.Config.Web.Port}");

        app.Services.GetRequiredService<SocketBroadcastSubscriber>();
        app.MapExternalApi();

        app.MapGet(
            "/callback/{pluginName}",
            async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
            {
                var plugin = registry.GetAll()
                                     .OfType<IHttpCallbackPlugin>()
                                     .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

                if (plugin is null)
                {
                    ctx.Response.StatusCode = 404;

                    return;
                }

                await plugin.HandleCallbackAsync(ctx, ct);
            }
        );

        app.MapPost(
            "/callback/{pluginName}",
            async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
            {
                var plugin = registry.GetAll()
                                     .OfType<IHttpCallbackPlugin>()
                                     .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

                if (plugin is null)
                {
                    ctx.Response.StatusCode = 404;

                    return;
                }

                await plugin.HandleCallbackAsync(ctx, ct);
            }
        );

        await app.RunAsync(ct);
    }
);
