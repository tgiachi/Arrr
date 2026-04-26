using System.Text.Json.Serialization;

namespace Arrr.Plugin.Todoist.Data.Internal;

internal record TodoistReminderResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("task_id")]
    public string TaskId { get; init; } = "";

    [JsonPropertyName("due")]
    public TodoistDue Due { get; init; } = new();
}
