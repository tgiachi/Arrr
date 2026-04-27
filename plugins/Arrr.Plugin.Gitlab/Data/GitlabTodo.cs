namespace Arrr.Plugin.Gitlab.Data;

public class GitlabTodo
{
    public int Id { get; set; }
    public string ActionName { get; set; } = "";
    public string TargetType { get; set; } = "";
    public GitlabTodoTarget Target { get; set; } = new();
    public GitlabTodoProject Project { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
}
