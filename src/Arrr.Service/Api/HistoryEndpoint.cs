using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class HistoryEndpoint
{
    public static IEndpointRouteBuilder MapHistoryApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/history",
            async (
                HttpContext ctx,
                IConfigService configService,
                INotificationHistoryService history,
                int page = 1,
                int limit = 50,
                string? search = null,
                string? source = null,
                CancellationToken ct = default
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                var result = await history.GetPageAsync(page, limit, search, source, ct);

                return Results.Ok(result);
            }
        );

        app.MapDelete(
            "/api/history",
            async (
                HttpContext ctx,
                IConfigService configService,
                INotificationHistoryService history,
                CancellationToken ct
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                await history.ClearAsync(ct);

                return Results.Ok();
            }
        );

        return app;
    }
}
