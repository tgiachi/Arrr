using System.ComponentModel;

namespace Arrr.Plugin.Healthcheck.Data;

public class EndpointConfig
{
    [Description("Display name shown in notifications")]
    public string Name { get; set; } = "";

    [Description("URL to probe (HTTP/HTTPS)")]
    public string Url { get; set; } = "";

    [Description("HTTP status codes considered healthy (default: 200–299)")]
    public List<int> ExpectedStatusCodes { get; set; } = [];
}
