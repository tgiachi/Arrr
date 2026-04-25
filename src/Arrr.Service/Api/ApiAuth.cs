using Arrr.Core.Interfaces;

namespace Arrr.Service.Api;

internal static class ApiAuth
{
    public static bool TryAuthenticate(HttpContext ctx, IConfigService configService, out IResult? error)
    {
        var key = configService.Config.ApiKey;

        if (key == "")
        {
            error = Results.Problem("API key not configured", statusCode: 503);

            return false;
        }

        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != key)
        {
            error = Results.Unauthorized();

            return false;
        }

        error = null;

        return true;
    }
}
