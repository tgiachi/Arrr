using System.Text.Json;
using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Github.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Github;

public class GithubSourcePlugin : IPollingPlugin, IConfigurablePlugin, ITestablePlugin, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HashSet<string> _seenIds = [];
    private readonly HttpClient _http;

    private GithubConfig _config = new();
    private IPluginContext? _context;
    private bool _firstPoll = true;

    public string Id => "com.arrr.plugin.github";
    public string Name => "GitHub";
    public string Version => VersionUtils.Get(typeof(GithubSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls GitHub notifications and publishes them to the event bus.";
    public string[] Categories => ["development", "git"];
    public string Icon => "🐙";
    public Type ConfigType => typeof(GithubConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes);

    public GithubSourcePlugin()
    {
        _http = new();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Arrr/1.0");
    }

    internal GithubSourcePlugin(HttpMessageHandler handler)
    {
        _http = new(handler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Arrr/1.0");
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            return new(false, "PersonalAccessToken not configured.");
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new("Bearer", _config.PersonalAccessToken);

            var response = await _http.SendAsync(request, ct);

            return response.IsSuccessStatusCode
                ? new(true, $"✓ OK ({(int)response.StatusCode})")
                : new(false, $"✗ {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, ex.Message);
        }
    }

    public void Dispose()
        => _http.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            return;
        }

        List<GithubNotification>? notifications;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/notifications?all=false");
            request.Headers.Authorization = new("Bearer", _config.PersonalAccessToken);

            var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _context?.Logger.LogWarning("GitHub API returned {Status}", response.StatusCode);

                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            notifications = JsonSerializer.Deserialize<List<GithubNotification>>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "GitHub notification fetch failed");

            return;
        }

        if (notifications is null)
        {
            return;
        }

        foreach (var n in notifications)
        {
            if (_firstPoll)
            {
                _seenIds.Add(n.Id);

                continue;
            }

            if (_seenIds.Add(n.Id))
            {
                var emoji = n.Subject.Type switch
                {
                    "PullRequest" => "🔀",
                    "Issue"       => "🐛",
                    "Release"     => "🚀",
                    "Discussion"  => "💬",
                    _             => "🔔"
                };

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        Id,
                        $"{emoji} {n.Repository.FullName}",
                        $"{n.Subject.Title}\nReason: {n.Reason}",
                        DateTimeOffset.UtcNow,
                        null,
                        Extras: new Dictionary<string, string>
                        {
                            ["github.repo"] = n.Repository.FullName,
                            ["github.subject_type"] = n.Subject.Type,
                            ["github.reason"] = n.Reason
                        }
                    ),
                    ct
                );
            }
        }

        if (_firstPoll)
        {
            _firstPoll = false;
        }
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<GithubConfig>(ct);

        if (string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            context.Logger.LogWarning("GitHub plugin: PersonalAccessToken not configured");
        }
        else
        {
            context.Logger.LogInformation("GitHub plugin ready, polling every {Interval} min", _config.PollIntervalMinutes);
        }
    }
}
