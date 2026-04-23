using Arrr.Core.Interfaces;

namespace Arrr.Service.Interfaces;

/// <summary>
/// Optional interface for plugins that need to handle HTTP callbacks (e.g. OAuth flows).
/// Implement this alongside ISourcePlugin to receive requests at /callback/{pluginName}.
/// </summary>
public interface IHttpCallbackPlugin : ISourcePlugin
{
    /// <summary>Handles an incoming HTTP request on the plugin's callback URL.</summary>
    Task HandleCallbackAsync(HttpContext httpContext, CancellationToken ct);
}
