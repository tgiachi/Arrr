using System.ComponentModel;

namespace Arrr.Sink.MacNotify.Data;

public class MacNotifyConfig
{
    [Description("Show the notification source as subtitle beneath the title")]
    public bool ShowSource { get; set; } = true;

    [Description("Alert sound name (e.g. 'default', 'Funk', 'Glass'). Set to empty string to disable sound.")]
    public string Sound { get; set; } = "default";
}
