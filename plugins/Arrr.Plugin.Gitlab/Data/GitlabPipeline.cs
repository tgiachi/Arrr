namespace Arrr.Plugin.Gitlab.Data;

public class GitlabPipeline
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public string Ref { get; set; } = "";
    public string WebUrl { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
