using System.Text.Json.Serialization;

namespace Arrr.Plugin.Todoist.Data.Internal;

internal record TodoistDue
{
    [JsonPropertyName("date")]
    public string Date { get; init; } = "";

    [JsonPropertyName("datetime")]
    public string? Datetime { get; init; }
}
