using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Github.Data;

public class GithubConfig
{
    [Description("GitHub Personal Access Token with 'notifications' scope"), Sensitive]
    public string PersonalAccessToken { get; set; } = "";

    [Description("How often to poll for new notifications, in minutes")]
    public int PollIntervalMinutes { get; set; } = 5;
}
