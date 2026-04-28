using System.ComponentModel;

namespace Arrr.Plugin.Gitlab.Data;

public class GitlabConfig
{
    [Description("How often to poll for new to-dos and pipeline statuses, in minutes")]
    public int PollIntervalMinutes { get; set; } = 5;

    [Description("One entry per GitLab instance (supports self-hosted and gitlab.com in parallel)")]
    public List<GitlabServerConfig> Servers { get; set; } = [];
}
