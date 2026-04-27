using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class DigestConfig
{
    [Description("Enable or disable the digest service entirely")]
    public bool Enabled { get; set; } = false;

    [Description("Scheduled digest entries to fire. Default: morning at 08:00 and evening at 19:00")]
    public List<DigestScheduleEntry> Schedule { get; set; } =
    [
        new DigestScheduleEntry
        {
            Label = "Morning Digest",
            TitleEmoji = "🌅",
            FireAt = "08:00",
            DayOffset = 0
        },
        new DigestScheduleEntry
        {
            Label = "Evening Digest",
            TitleEmoji = "🌙",
            FireAt = "19:00",
            DayOffset = 1
        }
    ];
}
