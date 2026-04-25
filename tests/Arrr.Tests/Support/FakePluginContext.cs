using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakePluginContext : IPluginContext
{
    private readonly Func<Type, object>? _configFactory;

    public string    ConfigPath  => "/tmp/fake.config";
    public ILogger   Logger      => NullLogger.Instance;
    public string    CallbackUrl => "/callback/fake";
    public IEventBus EventBus    { get; }

    public FakePluginContext(IEventBus eventBus, Func<Type, object>? configFactory = null)
    {
        EventBus        = eventBus;
        _configFactory  = configFactory;
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
