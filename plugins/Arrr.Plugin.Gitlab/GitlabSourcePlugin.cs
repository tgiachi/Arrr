using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Gitlab.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Gitlab;

public class GitlabSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HashSet<string> _seenIds = [];
    private readonly HttpClient _http;

    private GitlabConfig _config = new();
    private IPluginContext? _context;
    private bool _firstPoll = true;

    public string Id => "com.arrr.plugin.gitlab";
    public string Name => "GitLab";
    public string Version => VersionUtils.Get(typeof(GitlabSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls GitLab to-dos and publishes them to the event bus.";
    public string[] Categories => ["development", "git"];
    public string Icon => "🦊";
    public Type ConfigType => typeof(GitlabConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes);

    public GitlabSourcePlugin()
    {
        _http = new();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Arrr/1.0");
    }

    internal GitlabSourcePlugin(HttpMessageHandler handler)
    {
        _http = new(handler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Arrr/1.0");
    }

    public void Dispose()
        => _http.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            return;
        }

        List<GitlabTodo>? todos;

        try
        {
            var baseUrl = _config.GitlabUrl.TrimEnd('/');
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/todos?state=pending");
            request.Headers.Add("PRIVATE-TOKEN", _config.PersonalAccessToken);

            var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _context?.Logger.LogWarning("GitLab API returned {Status}", response.StatusCode);

                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            todos = JsonSerializer.Deserialize<List<GitlabTodo>>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "GitLab to-do fetch failed");

            return;
        }

        if (todos is null)
        {
            return;
        }

        foreach (var todo in todos)
        {
            var id = todo.Id.ToString();

            if (_firstPoll)
            {
                _seenIds.Add(id);

                continue;
            }

            if (_seenIds.Add(id))
            {
                var emoji = todo.TargetType switch
                {
                    "MergeRequest" => "🔀",
                    "Issue"        => "🐛",
                    "Epic"         => "🗂️",
                    "Commit"       => "📝",
                    _              => "🔔"
                };

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        Id,
                        $"{emoji} {todo.Project.PathWithNamespace}",
                        $"{todo.Target.Title}\nAction: {todo.ActionName.Replace('_', ' ')}",
                        DateTimeOffset.UtcNow,
                        null,
                        Url: todo.Target.WebUrl,
                        Extras: new Dictionary<string, string>
                        {
                            ["gitlab.project"]     = todo.Project.PathWithNamespace,
                            ["gitlab.target_type"] = todo.TargetType,
                            ["gitlab.action"]      = todo.ActionName
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
        _config = await context.LoadConfigAsync<GitlabConfig>(ct);

        if (string.IsNullOrEmpty(_config.PersonalAccessToken))
        {
            context.Logger.LogWarning("GitLab plugin: PersonalAccessToken not configured");
        }
        else
        {
            context.Logger.LogInformation(
                "GitLab plugin ready ({Url}), polling every {Interval} min",
                _config.GitlabUrl,
                _config.PollIntervalMinutes
            );
        }
    }
}
