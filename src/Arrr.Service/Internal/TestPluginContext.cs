using System.Text.Json;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Service.Internal;

internal sealed class TestPluginContext : IPluginContext, IDisposable
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JsonElement _configJson;

    public IEventBus EventBus { get; } = NullEventBus.Instance;
    public ILogger Logger { get; } = NullLogger.Instance;
    public string ConfigPath { get; } = "";
    public string CallbackUrl { get; } = "";
    public HttpClient Http { get; } = new();

    public TestPluginContext(JsonElement configJson)
    {
        _configJson = configJson;
    }

    public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        var config = _configJson.Deserialize<T>(_jsonOpts) ?? new T();
        // Safe on plaintext values — checks for "enc:" prefix before decrypting
        EncryptionUtils.ApplySensitiveFields(config, true);
        return Task.FromResult(config);
    }

    public Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
        => Task.CompletedTask;

    public void Dispose()
        => Http.Dispose();
}
