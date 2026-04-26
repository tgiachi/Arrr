using System.ComponentModel;
using Arrr.Core.Attributes;

namespace NtfySink.Data;

public class NtfySinkConfig
{
    [Description("ntfy server base URL")]
    public string ServerUrl { get; set; } = "https://ntfy.sh";

    [Description("ntfy topic name — required")]
    public string Topic { get; set; } = "";

    [Description("Bearer token for authentication (optional)"), Sensitive]
    public string AuthToken { get; set; } = "";

    [Description("Message priority: 1=min 2=low 3=default 4=high 5=max")]
    public int DefaultPriority { get; set; } = 3;

    [Description("Title template — {source}, {title}, {body} replaced at runtime")]
    public string TitleTemplate { get; set; } = "[{source}] {title}";
}
