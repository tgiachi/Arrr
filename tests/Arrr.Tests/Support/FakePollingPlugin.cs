using Arrr.Core.Interfaces;

namespace Arrr.Tests.Support;

internal class FakePollingPlugin : IPollingPlugin
{
    private readonly Func<int, Exception?>? _throwOnCall;

    public string Id => "com.test.polling";
    public string Name => "polling";
    public string Version => "1.0.0";
    public string Author => "test";
    public string Description => "fake polling plugin";
    public string[] Categories => [];
    public string Icon => "";

    public TimeSpan Interval { get; }
    public int PollCount { get; private set; }

    public FakePollingPlugin(TimeSpan? interval = null, Func<int, Exception?>? throwOnCall = null)
    {
        Interval = interval ?? TimeSpan.FromMilliseconds(10);
        _throwOnCall = throwOnCall;
    }

    public Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        PollCount++;
        var ex = _throwOnCall?.Invoke(PollCount);

        if (ex is not null)
        {
            throw ex;
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(IPluginContext context, CancellationToken ct)
        => Task.CompletedTask;
}
