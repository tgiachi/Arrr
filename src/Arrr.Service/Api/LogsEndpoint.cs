using Arrr.Core.Directories;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;

namespace Arrr.Service.Api;

internal static class LogsEndpoint
{
    private const int MaxLines = 300;

    public static IEndpointRouteBuilder MapLogsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/logs",
            async (
                HttpContext ctx,
                IConfigService configService,
                DirectoriesConfig directoriesConfig,
                CancellationToken ct
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var logsDir = directoriesConfig[DirectoryType.Logs];

                if (!Directory.Exists(logsDir))
                    return Results.Ok(Array.Empty<string>());

                var latest = Directory
                    .EnumerateFiles(logsDir, "*.txt")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();

                if (latest is null)
                    return Results.Ok(Array.Empty<string>());

                // FileShare.ReadWrite so Serilog can keep writing while we read
                await using var fs = new FileStream(
                    latest,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                using var reader = new StreamReader(fs);
                var text = await reader.ReadToEndAsync(ct);

                var lines = text
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .TakeLast(MaxLines)
                    .ToArray();

                return Results.Ok(lines);
            }
        );

        return app;
    }
}
