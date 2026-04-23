using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class ExternalNotifyEndpoint
{
    public static IEndpointRouteBuilder MapExternalApi(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/notify",
            async (HttpContext ctx, ExternalNotifyRequest req, IConfigService configService, IEventBus eventBus) =>
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

                if (string.IsNullOrWhiteSpace(req.Source) ||
                    string.IsNullOrWhiteSpace(req.Title) ||
                    string.IsNullOrWhiteSpace(req.Body))
                {
                    return Results.BadRequest("source, title and body are required");
                }

                var notification = new Notification(
                    Guid.NewGuid(),
                    req.Source,
                    req.Title,
                    req.Body,
                    DateTimeOffset.UtcNow,
                    req.IconUrl
                );

                await eventBus.PublishAsync(notification);

                return Results.NoContent();
            }
        );

        return app;
    }
}
