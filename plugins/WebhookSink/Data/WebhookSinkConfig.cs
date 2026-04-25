using System.ComponentModel;
using Arrr.Core.Attributes;

namespace WebhookSink.Data;

public class WebhookSinkConfig
{
    [Description("Webhook endpoint URL — required")]
    public string Url { get; set; } = "";

    [Description("Bearer token for authentication (optional)"), Sensitive]
    public string AuthToken { get; set; } = "";

    [Description("HTTP request timeout in seconds")]
    public int TimeoutSeconds { get; set; } = 10;
}
