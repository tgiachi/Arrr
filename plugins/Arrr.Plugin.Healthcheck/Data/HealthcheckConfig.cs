using System.ComponentModel;

namespace Arrr.Plugin.Healthcheck.Data;

public class HealthcheckConfig
{
    [Description("Endpoints to monitor")]
    public List<EndpointConfig> Endpoints { get; set; } = [];

    [Description("How often to probe all endpoints, in seconds")]
    public int PollIntervalSeconds { get; set; } = 60;

    [Description("Request timeout per endpoint, in seconds")]
    public int TimeoutSeconds { get; set; } = 10;
}
