using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arrr.Tests.Support;

internal class FakePluginContext : IPluginContext
{
    public string ConfigPath => "/tmp/fake.config";
    public ILogger Logger => NullLogger.Instance;
    public string CallbackUrl => "/callback/fake";
    public IEventBus EventBus { get; }

    public FakePluginContext(IEventBus eventBus)
    {
        EventBus = eventBus;
    }
}
