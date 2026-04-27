using Arrr.Core.Data.Config;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Internal;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Arrr.Service.Services;

internal class DigestService : BackgroundService
{
    private static readonly TimeSpan FireWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger = Log.ForContext<DigestService>();
    private readonly IPluginRegistry _pluginRegistry;
    private readonly IEventBus _eventBus;
    private readonly IConfigService _configService;
    private readonly TimeProvider _timeProvider;

    // key = "{entry.Label}:{localDate:yyyy-MM-dd}"
    private readonly HashSet<string> _firedKeys = [];
    private bool _firstPoll = true;

    public DigestService(
        IPluginRegistry pluginRegistry,
        IEventBus eventBus,
        IConfigService configService,
        TimeProvider timeProvider)
    {
        _pluginRegistry = pluginRegistry;
        _eventBus = eventBus;
        _configService = configService;
        _timeProvider = timeProvider;
    }

    internal DigestService(
        IPluginRegistry pluginRegistry,
        IEventBus eventBus,
        DigestConfig config,
        TimeProvider timeProvider)
    {
        _pluginRegistry = pluginRegistry;
        _eventBus = eventBus;
        _configService = null!;
        _timeProvider = timeProvider;
        _testConfig = config;
    }

    private readonly DigestConfig? _testConfig;

    private DigestConfig Config => _testConfig ?? _configService.Config.Digest;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAsync(stoppingToken);
            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    internal async Task PollAsync(CancellationToken ct)
    {
        var config = Config;

        if (!config.Enabled || config.Schedule.Count == 0)
        {
            if (_firstPoll)
            {
                _firstPoll = false;
            }

            return;
        }

        var localNow = _timeProvider.GetLocalNow();

        foreach (var entry in config.Schedule)
        {
            if (!TimeOnly.TryParse(entry.FireAt, out var fireTime))
            {
                _logger.Warning("Digest: invalid FireAt '{FireAt}' for entry '{Label}'", entry.FireAt, entry.Label);

                continue;
            }

            var localDate = DateOnly.FromDateTime(localNow.DateTime);
            var firedKey = $"{entry.Label}:{localDate:yyyy-MM-dd}";

            if (_firstPoll)
            {
                var windowEndTs = fireTime.Add(FireWindow).ToTimeSpan();

                if (localNow.TimeOfDay >= windowEndTs)
                {
                    _firedKeys.Add(firedKey);
                }

                continue;
            }

            if (_firedKeys.Contains(firedKey))
            {
                continue;
            }

            var windowStartTs = fireTime.ToTimeSpan();
            var windowEndSpan = fireTime.Add(FireWindow).ToTimeSpan();
            var currentTime = localNow.TimeOfDay;

            bool inWindow;

            if (windowStartTs <= windowEndSpan)
            {
                inWindow = currentTime >= windowStartTs && currentTime < windowEndSpan;
            }
            else
            {
                inWindow = currentTime >= windowStartTs || currentTime < windowEndSpan;
            }

            if (!inWindow)
            {
                continue;
            }

            _firedKeys.Add(firedKey);

            var forDate = localDate.AddDays(entry.DayOffset);
            var body = await BuildBodyAsync(forDate, ct);

            await _eventBus.PublishAsync(
                new Notification(
                    Guid.NewGuid(),
                    "arrr.digest",
                    $"{entry.TitleEmoji} {entry.Label}",
                    body,
                    _timeProvider.GetUtcNow(),
                    null
                ),
                ct
            );
        }

        if (_firstPoll)
        {
            _firstPoll = false;
        }
    }

    private async Task<string> BuildBodyAsync(DateOnly forDate, CancellationToken ct)
    {
        var providers = _pluginRegistry.GetAll().OfType<IDigestProvider>().ToList();

        if (providers.Count == 0)
        {
            return DigestFormatter.Format([new() { Title = "Digest", Items = [] }]);
        }

        var sections = new List<Arrr.Core.Data.Digest.DigestSection>();

        foreach (var provider in providers)
        {
            try
            {
                var section = await provider.GetDigestSectionAsync(forDate, ct);
                sections.Add(section);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Digest provider '{Title}' failed", provider.DigestSectionTitle);
            }
        }

        return DigestFormatter.Format(sections);
    }
}
