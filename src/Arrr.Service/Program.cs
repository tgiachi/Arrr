using Arrr.Core.Data.Config;
using Arrr.Core.Directories;
using Arrr.Core.Extensions.Logger;
using Arrr.Core.Interfaces;
using Arrr.Core.Json;
using Arrr.Core.Services;
using Arrr.Core.Types;
using Arrr.Service;
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
        )
        =>
    {
        rootDirectory ??= Environment.CurrentDirectory;


        var directoriesConfig = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());

        var loggerConfiguration = new LoggerConfiguration().MinimumLevel
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

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(directoriesConfig);
        builder.Services.AddSingleton<IConfigService, ConfigService>();

        builder.Logging.ClearProviders().AddSerilog();
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();

        var configService = host.Services.GetRequiredService<IConfigService>();
        await configService.LoadAsync(ct);

        await host.RunAsync(ct);
    }
);
