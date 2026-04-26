using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Stores incoming notifications and exposes a queryable history.
/// </summary>
public interface INotificationHistoryService
{
    /// <summary>Persists a notification to the history store.</summary>
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Deletes all stored history entries.</summary>
    Task ClearAsync(CancellationToken ct = default);

    /// <summary>Returns a paginated, optionally filtered slice of history (newest first).</summary>
    Task<HistoryPageResponse> GetPageAsync(
        int page,
        int limit,
        string? search = null,
        string? source = null,
        CancellationToken ct = default
    );
}
