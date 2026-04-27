using Arrr.Core.Data.Digest;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Supplies a named section of content for a digest notification.
/// Implement this interface to contribute items (e.g. calendar events, tasks)
/// to a digest fired by Arrr.Plugin.Digest.
/// </summary>
public interface IDigestProvider
{
    /// <summary>Human-readable section heading rendered in the notification body, e.g. "Today's Calendar".</summary>
    string SectionTitle { get; }

    /// <summary>
    /// Fetches items for <paramref name="forDate" />.
    /// Return a <see cref="DigestSection" /> with an empty <see cref="DigestSection.Items" /> list
    /// when there is no data — the formatter will render "No events." automatically.
    /// </summary>
    Task<DigestSection> GetSectionAsync(DateOnly forDate, CancellationToken ct);
}
