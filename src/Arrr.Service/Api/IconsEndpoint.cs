using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class IconsEndpoint
{
    public static IEndpointRouteBuilder MapIconsApi(this IEndpointRouteBuilder app)
    {
        // Returns all plugin and sink icons as base64-encoded PNG strings.
        // Intended for clients (e.g. arrr-tray) to fetch and cache all icons on connect.
        app.MapGet(
            "/api/icons",
            (HttpContext ctx, IConfigService configService, IPluginManager pluginManager, ISinkManager sinkManager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var plugins = pluginManager.GetAvailable()
                    .DistinctBy(p => p.Id)
                    .Select(p => (p.Id, Bytes: pluginManager.GetPluginIcon(p.Id)))
                    .Where(x => x.Bytes is not null)
                    .ToDictionary(x => x.Id, x => Convert.ToBase64String(x.Bytes!));

                var sinks = sinkManager.GetAvailable()
                    .DistinctBy(s => s.Id)
                    .Select(s => (s.Id, Bytes: sinkManager.GetSinkIcon(s.Id)))
                    .Where(x => x.Bytes is not null)
                    .ToDictionary(x => x.Id, x => Convert.ToBase64String(x.Bytes!));

                return Results.Ok(new { plugins, sinks });
            }
        );

        return app;
    }
}
