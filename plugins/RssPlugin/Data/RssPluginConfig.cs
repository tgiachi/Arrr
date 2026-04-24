using System.ComponentModel;

namespace RssPlugin.Data;

public class RssPluginConfig
{
    [Description("List of RSS/Atom feeds to poll. Each entry has a Url and a Label shown in notifications.")]
    public List<RssFeedConfig> Feeds { get; set; } = [];
}
