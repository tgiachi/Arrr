using System.Text.Json.Serialization;

namespace Arrr.Plugin.Todoist.Data.Internal;

internal record TodoistTaskResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("content")]
    public string Content { get; init; } = "";

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("project_id")]
    public string ProjectId { get; init; } = "";

    [JsonPropertyName("due")]
    public TodoistDue? Due { get; init; }
}
