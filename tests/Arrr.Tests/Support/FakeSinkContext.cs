using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakeSinkContext : ISinkContext
{
    private readonly Func<Type, object>? _configFactory;

    public ILogger Logger { get; } = NullLogger.Instance;
    public string ConfigPath { get; }

    public FakeSinkContext(string configPath = "", Func<Type, object>? configFactory = null)
    {
        ConfigPath = configPath;
        _configFactory = configFactory;
    }

    public Task<T> LoadConfigAsync<T>(CancellationToken ct = default) where T : new()
    {
        if (_configFactory is not null)
        {
            var result = _configFactory(typeof(T));
            if (result is T typed)
                return Task.FromResult(typed);
        }

        return Task.FromResult(new T());
    }

    public Task SaveConfigAsync<T>(T config, CancellationToken ct = default)
        => Task.CompletedTask;
}
