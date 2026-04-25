namespace Arrr.Tests.Support;

internal class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public void Advance(TimeSpan by)
    {
        _utcNow += by;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
