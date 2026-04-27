using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Gitlab.Data;

public class GitlabConfig
{
    [Description("GitLab instance URL (e.g. https://gitlab.com or https://gitlab.mycompany.com)")]
    public string GitlabUrl { get; set; } = "https://gitlab.com";

    [Description("GitLab Personal Access Token with 'api' scope"), Sensitive]
    public string PersonalAccessToken { get; set; } = "";

    [Description("How often to poll for new to-dos, in minutes")]
    public int PollIntervalMinutes { get; set; } = 5;
}
