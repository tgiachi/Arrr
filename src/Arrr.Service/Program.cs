using System.Text.Json;
using Arrr.Core.Data.Config;
using Arrr.Core.Data.Events;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Directories;
using Arrr.Core.Extensions.Logger;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Arrr.Service.Api;
using Arrr.Service.Hubs;
using Arrr.Service.Interfaces;
using Arrr.Service.Internal;
using Arrr.Service.Services;
using ConsoleAppFramework;
using Scalar.AspNetCore;
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
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        rootDirectory ??= Path.Combine(xdgDataHome, "arrr");

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
        builder.Services.AddSingleton<PluginContextFactory>();
        builder.Services.AddSingleton<NuGetPluginInstaller>();
        builder.Services.AddSingleton<IPluginInstaller>(sp => sp.GetRequiredService<NuGetPluginInstaller>());
        builder.Services.AddSingleton<PluginOrchestrator>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PluginOrchestrator>());
        builder.Services.AddSingleton<IPluginManager>(sp => sp.GetRequiredService<PluginOrchestrator>());
        builder.Services.AddSingleton<IRoutingHistoryService, RoutingHistoryService>();
        builder.Services.AddSingleton<IDndService, DndService>();
        builder.Services.AddSingleton<SinkOrchestrator>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<SinkOrchestrator>());
        builder.Services.AddSingleton<ISinkManager>(sp => sp.GetRequiredService<SinkOrchestrator>());
        builder.Services.AddSingleton<IConfigBackupService, ConfigBackupService>();
        builder.Services.AddSingleton<INotificationHistoryService>(
            _ => new NotificationHistoryService(Path.Combine(directoriesConfig.Root, "history.db"))
        );
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<DigestService>();
        builder.Services.AddHostedService<EventBusHostedService>();

        builder.Services.AddCors(
            opt =>
                opt.AddDefaultPolicy(
                    p =>
                        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
                )
        );

        builder.Services
               .AddSignalR()
               .AddJsonProtocol(
                   opt =>
                       opt.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase
               );

        builder.Services.AddHostedService<NotificationStreamService>();

        builder.Services.AddOpenApi();
        builder.Logging.ClearProviders().AddSerilog();

        var app = builder.Build();

        var configService = app.Services.GetRequiredService<IConfigService>();
        await configService.LoadAsync(ct);

        var eventBus = app.Services.GetRequiredService<IEventBus>();
        var historyService = app.Services.GetRequiredService<INotificationHistoryService>();

        eventBus.Subscribe<Notification>(
            async (n, token) =>
            {
                if (configService.Config.HistoryEnabled)
                {
                    await historyService.AddAsync(n, token);
                }
            }
        );

        eventBus.Subscribe<ArrStartedEvent>(
            async (evt, token) =>
                await eventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        "arrr",
                        "Arrr started",
                        $"Listening on port {configService.Config.Web.Port}",
                        evt.Timestamp,
                        null
                    ),
                    token
                )
        );

        app.Lifetime.ApplicationStarted.Register(
            () =>
                _ = eventBus.PublishAsync(new ArrStartedEvent(DateTimeOffset.UtcNow), ct)
        );

        app.Urls.Add($"http://0.0.0.0:{configService.Config.Web.Port}");

        app.UseCors();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseWebSockets();

        app.MapHub<NotificationStreamHub>("/stream");

        app.MapVersionApi();
        app.MapExternalApi();
        app.MapSinksApi();
        app.MapConfigBackupApi();
        app.MapLogsApi();
        app.MapDaemonConfigApi();
        app.MapHistoryApi();
        app.MapRoutingLogApi();
        app.MapDndApi();

        if (configService.Config.IsDebug)
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
            Log.Logger.Information("Debug mode: OpenAPI at /openapi/v1.json — Scalar UI at /scalar/v1");
        }

        app.MapPluginCallbacks(ct);

        await app.RunAsync(ct);
    }
);
