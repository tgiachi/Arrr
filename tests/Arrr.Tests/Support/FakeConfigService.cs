namespace Arrr.Tests.Support;

internal class FakeConfigService : IConfigService
{
    public ArrrConfig Config { get; }

    public FakeConfigService(string apiKey = "")
    {
        Config = new() { ApiKey = apiKey };
    }

    public Task LoadAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public void Save() { }
}
