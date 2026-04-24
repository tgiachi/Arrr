using Arrr.Core.Interfaces;
using Arrr.Service.Interfaces;

namespace Arrr.Service.Api;

internal static class PluginCallbackEndpoint
{
    public static IEndpointRouteBuilder MapPluginCallbacks(this IEndpointRouteBuilder app, CancellationToken ct)
    {
        app.MapGet(
            "/callback/{pluginName}",
            async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
            {
                var plugin = registry.GetAll()
                                     .OfType<IHttpCallbackPlugin>()
                                     .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

                if (plugin is null)
                {
                    ctx.Response.StatusCode = 404;

                    return;
                }

                await plugin.HandleCallbackAsync(ctx, ct);
            }
        );

        app.MapPost(
            "/callback/{pluginName}",
            async (string pluginName, HttpContext ctx, IPluginRegistry registry) =>
            {
                var plugin = registry.GetAll()
                                     .OfType<IHttpCallbackPlugin>()
                                     .FirstOrDefault(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

                if (plugin is null)
                {
                    ctx.Response.StatusCode = 404;

                    return;
                }

                await plugin.HandleCallbackAsync(ctx, ct);
            }
        );

        return app;
    }
}
