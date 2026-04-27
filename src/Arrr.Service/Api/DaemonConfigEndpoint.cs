using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class DaemonConfigEndpoint
{
    public static IEndpointRouteBuilder MapDaemonConfigApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/config",
            (HttpContext ctx, IConfigService configService) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                var c = configService.Config;

                return Results.Ok(
                    new DaemonConfigDto(
                        c.ApiKey,
                        c.IsDebug,
                        c.Web.Port,
                        c.Deduplication.Enabled,
                        c.Deduplication.WindowSeconds,
                        c.HistoryEnabled,
                        c.Digest
                    )
                );
            }
        );

        app.MapPut(
            "/api/config",
            (HttpContext ctx, DaemonConfigDto body, IConfigService configService) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                var c = configService.Config;
                c.ApiKey = body.ApiKey;
                c.IsDebug = body.IsDebug;
                c.Web.Port = body.Port;
                c.Deduplication.Enabled = body.DeduplicationEnabled;
                c.Deduplication.WindowSeconds = body.DeduplicationWindowSeconds;
                c.HistoryEnabled = body.HistoryEnabled;
                c.Digest = body.Digest;
                configService.Save();

                return Results.Ok();
            }
        );

        return app;
    }
}
