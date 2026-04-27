using System.ComponentModel;

namespace Arrr.Plugin.Digest.Data;

public class DigestEntry
{
    [Description("Display label used in the notification title (e.g. \"Morning Digest\")")]
    public string Label { get; set; } = "";

    [Description("Emoji prefix for the notification title (e.g. \"🌅\")")]
    public string TitleEmoji { get; set; } = "";

    [Description("Local time to fire this digest in HH:mm 24-hour format (e.g. \"08:00\")")]
    public string FireAt { get; set; } = "08:00";

    [Description("Day offset for calendar data: 0 = today, 1 = tomorrow")]
    public int DayOffset { get; set; } = 0;

    [Description("Section heading rendered in the notification body (e.g. \"Today's Calendar\")")]
    public string SectionHeading { get; set; } = "Calendar";
}
