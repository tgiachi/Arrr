using System.ComponentModel;
using Arrr.Core.Attributes;

namespace GotifySink.Data;

public class GotifySinkConfig
{
    [Description("Gotify server base URL")]
    public string ServerUrl { get; set; } = "http://localhost:8080";

    [Description("Application token — created in Gotify under Apps"), Sensitive]
    public string AppToken { get; set; } = "";

    [Description("Message priority (1=low … 10=high)")]
    public int DefaultPriority { get; set; } = 5;

    [Description("Title template — {source}, {title}, {body} replaced at runtime")]
    public string TitleTemplate { get; set; } = "[{source}] {title}";
}
