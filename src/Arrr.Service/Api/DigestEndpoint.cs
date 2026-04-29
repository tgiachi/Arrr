using Arrr.Core.Interfaces;
using Arrr.Service.Services;

namespace Arrr.Service.Api;

internal static class DigestEndpoint
{
    public static IEndpointRouteBuilder MapDigestApi(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/digest/trigger",
            async (
                HttpContext ctx,
                IConfigService configService,
                DigestService digestService,
                DateOnly? forDate,
                CancellationToken ct
            ) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                {
                    return error!;
                }

                await digestService.TriggerAsync(forDate, ct);
                return Results.Ok(new { triggered = true, forDate });
            }
        );

        return app;
    }
}
