using Arrr.Core.Data.Digest;

namespace Arrr.Core.Interfaces;

/// <summary>
/// Supplies a named section of content for a digest notification.
/// Implement this interface on any <see cref="ISourcePlugin"/> to contribute items
/// (e.g. calendar events, tasks) to the digest fired by <c>DigestService</c>.
/// </summary>
public interface IDigestProvider
{
    /// <summary>Human-readable heading rendered in the notification body (e.g. "Today's Calendar").</summary>
    string DigestSectionTitle { get; }

    /// <summary>
    /// Fetches items for <paramref name="forDate"/>.
    /// Return a <see cref="DigestSection"/> with an empty <see cref="DigestSection.Items"/> list
    /// when there is no data — the formatter will render "No events." automatically.
    /// </summary>
    Task<DigestSection> GetDigestSectionAsync(DateOnly forDate, CancellationToken ct);
}
