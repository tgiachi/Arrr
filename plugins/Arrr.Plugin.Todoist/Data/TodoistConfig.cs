using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Todoist.Data;

public class TodoistConfig
{
    [Description("Todoist API token (Settings → Integrations → Developer)"), Sensitive]
    public string ApiToken { get; set; } = "";

    [Description("Todoist filter string (e.g. \"today | overdue\", \"#Work & @urgent\")")]
    public string Filter { get; set; } = "today | overdue";

    [Description("Advance alert thresholds in minutes before the due time (e.g. [10, 15, 30])")]
    public List<int> AlertMinutes { get; set; } = [15];

    [Description("How often to poll Todoist, in minutes")]
    public int PollIntervalMinutes { get; set; } = 5;

    [Description("Also notify for explicit Todoist reminders (requires Todoist Pro/Business)")]
    public bool NotifyReminders { get; set; } = true;
}
