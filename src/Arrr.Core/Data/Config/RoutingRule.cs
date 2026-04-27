using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class RoutingRule
{
    [Description("Human-readable name for this rule (for display only)")]
    public string Name { get; set; } = "";

    [Description("Whether this rule is active")]
    public bool Enabled { get; set; } = true;

    [Description("Source plugin ID to match. Supports trailing wildcard, e.g. \"com.arrr.plugin.*\". Empty = match any source.")]
    public string SourcePattern { get; set; } = "";

    [Description("Case-insensitive substring that must appear in the notification title. Empty = match any title.")]
    public string TitleContains { get; set; } = "";

    [Description("Case-insensitive substring that must appear in the notification body. Empty = match any body.")]
    public string BodyContains { get; set; } = "";

    [Description("Minimum priority level to match (0=Normal, 1=High, 2=Critical). 0 matches all.")]
    public int MinPriority { get; set; } = 0;

    [Description("If true, block the notification entirely (do not dispatch to any sink).")]
    public bool Block { get; set; } = false;

    [Description("Restrict delivery to these sink IDs. Empty = allow all running sinks. Ignored when Block=true.")]
    public List<string> AllowSinks { get; set; } = [];

    [Description("Extra key/value conditions that must all match (AND). Each entry checks Notification.Extras[Key] contains Value.")]
    public List<ExtraCondition> ExtraConditions { get; set; } = [];

    [Description("Local time from which this rule is active, in HH:mm 24-hour format (e.g. \"22:00\"). Empty = no lower bound.")]
    public string ActiveFrom { get; set; } = "";

    [Description("Local time until which this rule is active, in HH:mm 24-hour format (e.g. \"08:00\"). Empty = no upper bound. Supports midnight crossing.")]
    public string ActiveTo { get; set; } = "";
}
