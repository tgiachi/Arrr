namespace Arrr.Plugin.Github.Data;

public class GithubNotification
{
    public string Id { get; set; } = "";
    public GithubSubject Subject { get; set; } = new();
    public string Reason { get; set; } = "";
    public GithubRepository Repository { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}
