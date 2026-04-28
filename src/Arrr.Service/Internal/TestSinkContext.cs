using System.Text.Json;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Service.Internal;

internal sealed class TestSinkContext : ISinkContext
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JsonElement _configJson;

    public string ConfigPath { get; } = "";
    public ILogger Logger { get; } = NullLogger.Instance;

    public TestSinkContext(JsonElement configJson)
    {
        _configJson = configJson;
    }

    public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        var config = _configJson.Deserialize<T>(JsonOpts) ?? new T();
        EncryptionUtils.ApplySensitiveFields(config, true);

        return Task.FromResult(config);
    }

    public Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
        => Task.CompletedTask;
}
