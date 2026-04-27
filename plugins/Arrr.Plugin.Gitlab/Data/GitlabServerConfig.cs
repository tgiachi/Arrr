using System.ComponentModel;
using Arrr.Core.Attributes;

namespace Arrr.Plugin.Gitlab.Data;

public class GitlabServerConfig
{
    [Description("GitLab instance URL (e.g. https://gitlab.com or https://gitlab.mycompany.com)")]
    public string GitlabUrl { get; set; } = "https://gitlab.com";

    [Description("GitLab Personal Access Token with 'api' scope"), Sensitive]
    public string PersonalAccessToken { get; set; } = "";

    [Description("Project paths to monitor for CI pipeline status (e.g. [\"namespace/project\"])")]
    public List<string> PipelineProjects { get; set; } = [];
}
