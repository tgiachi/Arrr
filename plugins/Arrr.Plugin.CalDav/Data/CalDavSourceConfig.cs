using System.ComponentModel;

namespace CalDavSource.Data;

public class CalDavSourceConfig
{
    [Description("One entry per calendar to monitor (Office 365, Google, Nextcloud, iCloud…)")]
    public List<CalDavCalendarConfig> Calendars { get; set; } = [];

    [Description("List of advance alert times in minutes, e.g. [10, 15, 30]")]
    public List<int> AlertMinutes { get; set; } = [15];

    [Description("How often to re-fetch all calendars, in minutes")]
    public int PollIntervalMinutes { get; set; } = 15;

    [Description("How many hours ahead to look for upcoming events")]
    public int LookaheadHours { get; set; } = 24;

    [Description("Section heading used in digest notifications (e.g. \"Today's Calendar\")")]
    public string DigestSectionTitle { get; set; } = "Calendar";
}
