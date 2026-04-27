using Arrr.Core.Data.Config;
using Arrr.Core.Data.Digest;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Services;
using Arrr.Tests.Support;

namespace Arrr.Tests.Services;

[TestFixture]
public class DigestServiceTests
{
    private FakeEventBus _eventBus = null!;
    private FakeTimeProvider _timeProvider = null!;
    private FakePluginRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _eventBus = new();
        // 06:00 UTC — GetLocalNow() derives local time via base class
        _timeProvider = new(new DateTimeOffset(2026, 4, 27, 6, 0, 0, TimeSpan.Zero));
        _registry = new();
    }

    // ── first poll is seed-only ───────────────────────────────────────────────

    [Test]
    public async Task PollAsync_FirstPoll_NeverPublishes()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow:HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── fires within window ───────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_AtFireTime_PublishesDigest()
    {
        var localNow = _timeProvider.GetLocalNow();
        var provider = new FakeDigestProvider("Today's Calendar", [new DigestItem { Text = "Team standup" }]);
        _registry.Add(provider);

        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("Morning Digest"));
        Assert.That(n.Body, Does.Contain("Team standup"));
    }

    // ── does not fire twice same day ──────────────────────────────────────────

    [Test]
    public async Task PollAsync_InsideWindow_DoesNotFireTwice()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token); // fires

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await service.PollAsync(cts.Token); // still inside window, must NOT fire again

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
    }

    // ── outside window: does not fire ────────────────────────────────────────

    [Test]
    public async Task PollAsync_AfterWindowExpired_DoesNotFire()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(10)); // past window end
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── before window: does not fire ─────────────────────────────────────────

    [Test]
    public async Task PollAsync_BeforeFireWindow_DoesNotFire()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(30):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token); // seed

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── restart after window passed: key seeded, no re-fire ──────────────────

    [Test]
    public async Task PollAsync_RestartAfterWindowPassed_DoesNotRefireSameDay()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(-6):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token); // seeds the already-past key

        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        await service.PollAsync(cts.Token); // key seeded → no fire

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── no schedule → nothing published ──────────────────────────────────────

    [Test]
    public async Task PollAsync_NoSchedule_PublishesNothing()
    {
        var config = new DigestConfig { Enabled = true, Schedule = [] };
        var service = new DigestService(_registry, _eventBus, config, _timeProvider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── disabled → nothing published ─────────────────────────────────────────

    [Test]
    public async Task PollAsync_Disabled_PublishesNothing()
    {
        var localNow = _timeProvider.GetLocalNow();
        var config = new DigestConfig
        {
            Enabled = false,
            Schedule = [new DigestScheduleEntry { Label = "Morning Digest", TitleEmoji = "🌅", FireAt = $"{localNow.AddMinutes(1):HH:mm}", DayOffset = 0 }]
        };
        var service = new DigestService(_registry, _eventBus, config, _timeProvider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Is.Empty);
    }

    // ── no providers → empty body with "No events." ───────────────────────────

    [Test]
    public async Task PollAsync_NoProviders_PublishesWithNoEventsBody()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body, Does.Contain("No events."));
    }

    // ── multiple providers → all sections merged ──────────────────────────────

    [Test]
    public async Task PollAsync_MultipleProviders_AllSectionsInBody()
    {
        var localNow = _timeProvider.GetLocalNow();
        _registry.Add(new FakeDigestProvider("Calendar", [new DigestItem { Text = "Standup" }]));
        _registry.Add(new FakeDigestProvider("Tasks", [new DigestItem { Text = "Review PR" }]));

        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(_eventBus.Published, Has.Count.EqualTo(1));
        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Body, Does.Contain("Standup"));
        Assert.That(n.Body, Does.Contain("Review PR"));
    }

    // ── notification source ID ────────────────────────────────────────────────

    [Test]
    public async Task PollAsync_PublishedNotification_HasCorrectSourceId()
    {
        var localNow = _timeProvider.GetLocalNow();
        var service = MakeService(fireAt: $"{localNow.AddMinutes(1):HH:mm}");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Source, Is.EqualTo("arrr.digest"));
    }

    // ── notification title has emoji + label ──────────────────────────────────

    [Test]
    public async Task PollAsync_PublishedNotification_TitleContainsEmojiAndLabel()
    {
        var localNow = _timeProvider.GetLocalNow();
        var config = new DigestConfig
        {
            Enabled = true,
            Schedule =
            [
                new DigestScheduleEntry
                {
                    Label = "Evening Digest",
                    TitleEmoji = "🌙",
                    FireAt = $"{localNow.AddMinutes(1):HH:mm}",
                    DayOffset = 1
                }
            ]
        };
        var service = new DigestService(_registry, _eventBus, config, _timeProvider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        var n = (Notification)_eventBus.Published[0];
        Assert.That(n.Title, Does.Contain("🌙"));
        Assert.That(n.Title, Does.Contain("Evening Digest"));
    }

    // ── provider receives correct forDate with dayOffset ──────────────────────

    [Test]
    public async Task PollAsync_EveningDigest_ProviderReceivesTomorrow()
    {
        var localNow = _timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var tomorrow = today.AddDays(1);

        var provider = new FakeDigestProvider("Tomorrow's Calendar", []);
        _registry.Add(provider);

        var config = new DigestConfig
        {
            Enabled = true,
            Schedule =
            [
                new DigestScheduleEntry
                {
                    Label = "Evening Digest",
                    TitleEmoji = "🌙",
                    FireAt = $"{localNow.AddMinutes(1):HH:mm}",
                    DayOffset = 1
                }
            ]
        };
        var service = new DigestService(_registry, _eventBus, config, _timeProvider);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await service.PollAsync(cts.Token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        await service.PollAsync(cts.Token);

        Assert.That(provider.LastForDate, Is.EqualTo(tomorrow));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private DigestService MakeService(string fireAt)
    {
        var config = new DigestConfig
        {
            Enabled = true,
            Schedule =
            [
                new DigestScheduleEntry
                {
                    Label = "Morning Digest",
                    TitleEmoji = "🌅",
                    FireAt = fireAt,
                    DayOffset = 0
                }
            ]
        };

        return new DigestService(_registry, _eventBus, config, _timeProvider);
    }

    private sealed class FakePluginRegistry : IPluginRegistry
    {
        private readonly List<ISourcePlugin> _plugins = [];

        public void Add(ISourcePlugin plugin)
            => _plugins.Add(plugin);

        public IReadOnlyList<ISourcePlugin> GetAll()
            => _plugins;

        public void Register(ISourcePlugin plugin)
            => _plugins.Add(plugin);

        public void Unregister(string pluginId)
            => _plugins.RemoveAll(p => p.Id == pluginId);
    }

    private sealed class FakeDigestProvider : ISourcePlugin, IDigestProvider
    {
        private readonly List<DigestItem> _items;

        public DateOnly LastForDate { get; private set; }

        public FakeDigestProvider(string sectionTitle, List<DigestItem> items)
        {
            DigestSectionTitle = sectionTitle;
            _items = items;
        }

        public string Id => $"fake.{DigestSectionTitle}";
        public string Name => DigestSectionTitle;
        public string Version => "1.0";
        public string Author => "test";
        public string Description => "";
        public string[] Categories => [];
        public string Icon => "";
        public string[] Platforms => [];

        public string DigestSectionTitle { get; }

        public Task<DigestSection> GetDigestSectionAsync(DateOnly forDate, CancellationToken ct)
        {
            LastForDate = forDate;

            return Task.FromResult(new DigestSection { Title = DigestSectionTitle, Items = _items });
        }

        public Task StartAsync(IPluginContext context, CancellationToken ct)
            => Task.CompletedTask;
    }
}
