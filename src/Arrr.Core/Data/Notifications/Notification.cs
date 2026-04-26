using Arrr.Core.Interfaces;
using Arrr.Core.Types;

namespace Arrr.Core.Data.Notifications;

public record Notification(
    Guid Id,
    string Source,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    string? IconUrl,
    NotificationPriority Priority = NotificationPriority.Normal,
    string? Url = null,
    IReadOnlyDictionary<string, string>? Extras = null
) : IArrrEvent
{
    public string? GetExtra(string key)
        => Extras?.GetValueOrDefault(key);

    public bool HasExtra(string key)
        => Extras?.ContainsKey(key) == true;

    /// <summary>Maps to Bark level: passive/active/timeSensitive/critical.</summary>
    public string ToBarkLevel()
        => Priority switch
        {
            NotificationPriority.Low      => "passive",
            NotificationPriority.Normal   => "active",
            NotificationPriority.High     => "timeSensitive",
            NotificationPriority.Critical => "critical",
            _                             => "active"
        };

    /// <summary>Maps to D-Bus urgency hint: 0=low, 1=normal, 2=critical.</summary>
    public byte ToDbusUrgency()
        => Priority switch
        {
            NotificationPriority.Low      => 0,
            NotificationPriority.Normal   => 1,
            NotificationPriority.High     => 1,
            NotificationPriority.Critical => 2,
            _                             => 1
        };

    /// <summary>Maps to Gotify priority (1–10).</summary>
    public int ToGotifyPriority()
        => Priority switch
        {
            NotificationPriority.Low      => 1,
            NotificationPriority.Normal   => 5,
            NotificationPriority.High     => 8,
            NotificationPriority.Critical => 10,
            _                             => 5
        };

    /// <summary>Maps to ntfy priority string: min/low/default/high/urgent.</summary>
    public string ToNtfyPriority()
        => Priority switch
        {
            NotificationPriority.Low      => "low",
            NotificationPriority.Normal   => "default",
            NotificationPriority.High     => "high",
            NotificationPriority.Critical => "urgent",
            _                             => "default"
        };

    /// <summary>Maps to Pushover priority: -2=Low, 0=Normal, 1=High, 2=Critical.</summary>
    public int ToPushoverPriority()
        => Priority switch
        {
            NotificationPriority.Low      => -2,
            NotificationPriority.Normal   => 0,
            NotificationPriority.High     => 1,
            NotificationPriority.Critical => 2,
            _                             => 0
        };
}
