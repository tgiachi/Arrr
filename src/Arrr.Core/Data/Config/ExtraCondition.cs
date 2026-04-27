using System.ComponentModel;

namespace Arrr.Core.Data.Config;

public class ExtraCondition
{
    [Description("Extras key to match (e.g. \"todoist.project_id\")")]
    public string Key { get; set; } = "";

    [Description("Case-insensitive substring to match against the key's value. Empty = key must exist with any value.")]
    public string Value { get; set; } = "";
}
