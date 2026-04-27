using Arrr.Core.Interfaces;
using Arrr.Service.Interfaces;

namespace Arrr.Service.Api;

internal static class RoutingLogEndpoint
{
    public static IEndpointRouteBuilder MapRoutingLogApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/routing/log",
            (HttpContext ctx, IConfigService configService, IRoutingHistoryService history, int limit = 50) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                return Results.Ok(history.GetRecent(limit));
            }
        );

        return app;
    }
}
