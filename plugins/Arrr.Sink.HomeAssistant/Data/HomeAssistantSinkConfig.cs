using System.ComponentModel;
using Arrr.Core.Attributes;

namespace HomeAssistantSink.Data;

public class HomeAssistantSinkConfig
{
    [Description("Home Assistant base URL (e.g. http://homeassistant.local:8123)")]
    public string BaseUrl { get; set; } = "http://homeassistant.local:8123";

    [Description("Long-lived access token — created in HA under Profile → Security"), Sensitive]
    public string AccessToken { get; set; } = "";

    [Description("Notify service name (e.g. 'notify' for the default group, or 'mobile_app_myphone')")]
    public string NotifyService { get; set; } = "notify";

    [Description("Title template — {source}, {title}, {body} replaced at runtime")]
    public string TitleTemplate { get; set; } = "[{source}] {title}";
}
