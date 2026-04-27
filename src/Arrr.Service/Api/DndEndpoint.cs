using Arrr.Core.Interfaces;
using Arrr.Service.Interfaces;

namespace Arrr.Service.Api;

internal static class DndEndpoint
{
    public static IEndpointRouteBuilder MapDndApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/dnd",
            (HttpContext ctx, IConfigService configService, IDndService dnd) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                return Results.Ok(new { enabled = dnd.IsEnabled });
            }
        );

        app.MapPut(
            "/api/dnd",
            (HttpContext ctx, IConfigService configService, IDndService dnd, DndBody body) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                dnd.Set(body.Enabled);
                return Results.Ok(new { enabled = dnd.IsEnabled });
            }
        );

        return app;
    }
}

internal record DndBody(bool Enabled);
