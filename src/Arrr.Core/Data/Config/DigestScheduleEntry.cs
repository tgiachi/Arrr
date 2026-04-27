using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class DigestScheduleEntry
{
    [Description("Display label used in the notification title (e.g. \"Morning Digest\")")]
    public string Label { get; set; } = "";

    [Description("Emoji prefix for the notification title (e.g. \"🌅\")")]
    public string TitleEmoji { get; set; } = "";

    [Description("Local time to fire in HH:mm 24-hour format (e.g. \"08:00\")")]
    public string FireAt { get; set; } = "08:00";

    [Description("Day offset for providers: 0 = today, 1 = tomorrow")]
    public int DayOffset { get; set; } = 0;
}
