using Microsoft.Extensions.Logging;

namespace Arrr.Core.Interfaces;

/// <summary>Runtime context injected into each sink at startup.</summary>
public interface ISinkContext
{
    string ConfigPath { get; }
    ILogger Logger { get; }

    Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new();
    Task SaveConfigAsync<T>(T config, CancellationToken ct = default);
}
