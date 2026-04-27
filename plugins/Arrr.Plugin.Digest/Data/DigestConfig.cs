using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Digest.Data;

public class DigestConfig
{
    [Description("Direct .ics / CalDAV subscription URL to fetch calendar events from")]
    public string CalendarUrl { get; set; } = "";

    [Description("HTTP Basic Auth username (leave empty if not required)")]
    public string CalendarUsername { get; set; } = "";

    [Description("HTTP Basic Auth password (leave empty if not required)")]
    [Sensitive]
    public string CalendarPassword { get; set; } = "";

    [Description("Scheduled digests to fire. Default: morning (today) at 08:00 and evening (tomorrow) at 19:00")]
    public List<DigestEntry> Digests { get; set; } =
    [
        new DigestEntry
        {
            Label = "Morning Digest",
            TitleEmoji = "🌅",
            FireAt = "08:00",
            DayOffset = 0,
            SectionHeading = "Today's Calendar"
        },
        new DigestEntry
        {
            Label = "Evening Digest",
            TitleEmoji = "🌙",
            FireAt = "19:00",
            DayOffset = 1,
            SectionHeading = "Tomorrow's Calendar"
        }
    ];

    [Description("How often (minutes) the plugin checks if a digest is due. Keep ≤ 4.")]
    public int PollIntervalMinutes { get; set; } = 1;
}
