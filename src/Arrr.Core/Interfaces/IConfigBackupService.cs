using System.Text.Json;

namespace Arrr.Core.Interfaces;

public interface IConfigBackupService
{
    Task<Dictionary<string, JsonElement>> ExportAsync(CancellationToken ct);
    Task<int> ImportAsync(Dictionary<string, JsonElement> configs, CancellationToken ct);
}
