using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Sink.Bark.Data;

public class BarkConfig
{
    [Description("Bark server base URL (default: official Bark server)")]
    public string ServerUrl { get; set; } = "https://api.day.app";

    [Description("Device key shown in the Bark app"), Sensitive]
    public string DeviceKey { get; set; } = "";

    [Description("Notification level: active, timeSensitive, passive")]
    public string Level { get; set; } = "active";

    [Description("Sound name (e.g. 'minuet', 'bell', empty = default)")]
    public string Sound { get; set; } = "";

    [Description("Notification group name for grouping in iOS notification center")]
    public string Group { get; set; } = "Arrr";
}
