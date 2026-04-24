using System.Text.Json;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class SinksEndpoint
{
    public static IEndpointRouteBuilder MapSinksApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/sinks",
            (HttpContext ctx, IConfigService configService, ISinkManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                return Results.Ok(manager.GetAvailable());
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/enable",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.EnableAsync(sinkId, ct);
                return Results.Ok(new { sinkId, enabled = true });
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/disable",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.DisableAsync(sinkId);
                return Results.Ok(new { sinkId, enabled = false });
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/reload",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.ReloadAsync(sinkId, ct);
                return Results.Ok(new { sinkId, reloaded = true });
            }
        );

        app.MapGet(
            "/api/sinks/{sinkId}/config",
            async (HttpContext ctx, string sinkId, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var response = await manager.GetSinkConfigAsync(sinkId, ct);
                return response is null
                    ? Results.NotFound(new { sinkId, error = "Sink not found or has no config." })
                    : Results.Ok(response);
            }
        );

        app.MapPost(
            "/api/sinks/{sinkId}/config",
            async (HttpContext ctx, string sinkId, JsonElement body, IConfigService configService, ISinkManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                try
                {
                    await manager.SaveSinkConfigAsync(sinkId, body, ct);
                    return Results.Ok(new { sinkId, saved = true });
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new { sinkId, error = "Sink not found." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { sinkId, error = ex.Message });
                }
            }
        );

        return app;
    }
}
