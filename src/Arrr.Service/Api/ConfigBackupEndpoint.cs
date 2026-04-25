using System.Text.Json;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class ConfigBackupEndpoint
{
    public static IEndpointRouteBuilder MapConfigBackupApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/config/backup",
            async (
                HttpContext ctx,
                IConfigService configService,
                IConfigBackupService backupService,
                CancellationToken ct
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                var configs = await backupService.ExportAsync(ct);

                return Results.Ok(configs);
            }
        );

        app.MapPost(
            "/api/config/restore",
            async (
                HttpContext ctx,
                Dictionary<string, JsonElement> body,
                IConfigService configService,
                IConfigBackupService backupService,
                CancellationToken ct
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                var count = await backupService.ImportAsync(body, ct);

                return Results.Ok(new { restored = count });
            }
        );

        return app;
    }
}
