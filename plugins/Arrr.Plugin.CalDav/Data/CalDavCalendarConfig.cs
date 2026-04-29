using System.ComponentModel;
using Arrr.Core.Attributes;

namespace CalDavSource.Data;

public class CalDavCalendarConfig
{
    [Description("Display name for this calendar (shown in notifications)")]
    public string Name { get; set; } = "";

    [Description("Direct .ics subscription URL (Office 365, Google Calendar, Nextcloud, iCloud, etc.)")]
    public string CalendarUrl { get; set; } = "";

    [Description("HTTP Basic Auth username (optional — leave empty if URL already contains a token)")]
    public string Username { get; set; } = "";

    [Sensitive, Description("HTTP Basic Auth password (optional, stored encrypted)")]
    public string Password { get; set; } = "";
}
