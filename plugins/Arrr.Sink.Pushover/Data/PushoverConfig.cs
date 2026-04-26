using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Sink.Pushover.Data;

public class PushoverConfig
{
    [Description("Pushover API token (from your Pushover application)"), Sensitive]
    public string ApiToken { get; set; } = "";

    [Description("Pushover user key"), Sensitive]
    public string UserKey { get; set; } = "";

    [Description("Message priority: -2 (silent), -1 (quiet), 0 (normal), 1 (high), 2 (emergency)")]
    public int Priority { get; set; } = 0;

    [Description("Notification sound name (e.g. 'bike', 'bugle', 'magic', empty = account default)")]
    public string Sound { get; set; } = "";
}
