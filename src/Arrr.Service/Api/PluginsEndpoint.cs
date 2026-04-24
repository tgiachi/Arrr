using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;
using Arrr.Service.Interfaces;

namespace Arrr.Service.Api;

internal static class PluginsEndpoint
{
    public static IEndpointRouteBuilder MapPluginsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/plugins",
            (HttpContext ctx, IConfigService configService, IPluginRegistry registry) =>
            {
                var key = configService.Config.ApiKey;

                if (key == "")
                {
                    return Results.Problem("API key not configured", statusCode: 503);
                }

                if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != key)
                {
                    return Results.Unauthorized();
                }

                var plugins = registry.GetAll()
                                      .Select(
                                          p => new PluginInfoResponse(
                                              p.Id, p.Name, p.Version,
                                              p.Author, p.Description,
                                              p.Categories, p.Icon
                                          )
                                      )
                                      .ToList();

                return Results.Ok(plugins);
            }
        );

        return app;
    }
}
