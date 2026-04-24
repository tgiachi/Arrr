using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Interfaces;
using Arrr.Service.Internal;

namespace Arrr.Service.Api;

internal static class PluginsEndpoint
{
    public static IEndpointRouteBuilder MapPluginsApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/plugins",
            (HttpContext ctx, IConfigService configService, IPluginRegistry registry) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var plugins = registry.GetAll()
                                      .Select(
                                          p => new PluginInfoResponse(
                                              p.Id, p.Name, p.Version, p.Author,
                                              p.Description, p.Categories, p.Icon
                                          )
                                      )
                                      .ToList();

                return Results.Ok(plugins);
            }
        );

        app.MapGet(
            "/api/plugins/available",
            (HttpContext ctx, IConfigService configService, IPluginManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                return Results.Ok(manager.GetAvailable());
            }
        );

        app.MapPost(
            "/api/plugins/{pluginId}/enable",
            async (HttpContext ctx, string pluginId, IConfigService configService, IPluginManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.EnableAsync(pluginId, ct);
                return Results.Ok(new { pluginId, enabled = true });
            }
        );

        app.MapPost(
            "/api/plugins/{pluginId}/disable",
            async (HttpContext ctx, string pluginId, IConfigService configService, IPluginManager manager) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.DisableAsync(pluginId);
                return Results.Ok(new { pluginId, enabled = false });
            }
        );

        app.MapPost(
            "/api/plugins/{pluginId}/reload",
            async (HttpContext ctx, string pluginId, IConfigService configService, IPluginManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.ReloadAsync(pluginId, ct);
                return Results.Ok(new { pluginId, reloaded = true });
            }
        );

        app.MapPost(
            "/api/plugins/reload/all",
            async (HttpContext ctx, IConfigService configService, IPluginManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await manager.ReloadAllAsync(ct);
                return Results.Ok(new { reloaded = true });
            }
        );

        app.MapPost(
            "/api/plugins/install",
            async (HttpContext ctx, InstallPluginRequest request, IConfigService configService, IPluginInstaller installer, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await installer.InstallAsync(request.PackageId, request.Version, ct);
                return Results.Ok(new { request.PackageId, installed = true });
            }
        );

        app.MapPost(
            "/api/plugins/{packageId}/uninstall",
            async (HttpContext ctx, string packageId, IConfigService configService, IPluginInstaller installer, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                await installer.UninstallAsync(packageId, ct);
                return Results.Ok(new { packageId, uninstalled = true });
            }
        );

        app.MapGet(
            "/api/plugins/{pluginId}/config",
            async (HttpContext ctx, string pluginId, IConfigService configService, IPluginManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                var config = await manager.GetPluginConfigAsync(pluginId, ct);
                return config is null
                    ? Results.NotFound(new { pluginId, error = "Plugin not found or has no config." })
                    : Results.Ok(config);
            }
        );

        app.MapPost(
            "/api/plugins/{pluginId}/config",
            async (HttpContext ctx, string pluginId, JsonElement body, IConfigService configService, IPluginManager manager, CancellationToken ct) =>
            {
                if (!ApiAuth.TryAuthenticate(ctx, configService, out var error))
                    return error!;

                try
                {
                    await manager.SavePluginConfigAsync(pluginId, body, ct);
                    return Results.Ok(new { pluginId, saved = true });
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound(new { pluginId, error = "Plugin not found." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { pluginId, error = ex.Message });
                }
            }
        );

        return app;
    }
}
