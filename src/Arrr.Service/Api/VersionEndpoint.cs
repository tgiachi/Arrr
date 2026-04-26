using System.Runtime.InteropServices;
using Arrr.Core.Data.Api;
using Arrr.Core.Utils;

namespace Arrr.Service.Api;

internal static class VersionEndpoint
{
    public static IEndpointRouteBuilder MapVersionApi(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/version",
            () =>
            {
                var version = VersionUtils.Get(typeof(VersionEndpoint));
                var runtime = RuntimeInformation.FrameworkDescription;
                var os = RuntimeInformation.OSDescription;

                return Results.Ok(new VersionResponse(version, runtime, os));
            }
        );

        return app;
    }
}
