namespace Arrr.Core.Interfaces;

/// <summary>
/// Implemented by plugins that can receive arbitrary payloads from the outside world
/// via POST /api/plugins/{id}/callback.
/// </summary>
public interface ICallbackPlugin
{
    Task HandleCallbackAsync(string body, CancellationToken ct);
}
