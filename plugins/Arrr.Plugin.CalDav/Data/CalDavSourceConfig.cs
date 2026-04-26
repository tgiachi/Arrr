using System.ComponentModel;
using Arrr.Core.Attributes;

namespace CalDavSource.Data;

public class CalDavSourceConfig
{
    [Description("Direct .ics subscription URL (Google Calendar, Nextcloud, iCloud, etc.)")]
    public string CalendarUrl { get; set; } = "";

    [Description("HTTP Basic Auth username (optional)")]
    public string Username { get; set; } = "";

    [Description("HTTP Basic Auth password (optional)"), Sensitive]
    public string Password { get; set; } = "";

    [Description("List of advance alert times in minutes, e.g. [10, 15, 30]")]
    public List<int> AlertMinutes { get; set; } = [15];

    [Description("How often to re-fetch the calendar, in minutes")]
    public int PollIntervalMinutes { get; set; } = 15;

    [Description("How many hours ahead to look for upcoming events")]
    public int LookaheadHours { get; set; } = 24;
}
