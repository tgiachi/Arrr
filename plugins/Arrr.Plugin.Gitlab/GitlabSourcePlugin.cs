using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Arrr.Plugin.Gitlab.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Gitlab;

public class GitlabSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    private static readonly HashSet<string> TerminalStatuses = ["success", "failed", "canceled"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HashSet<string> _seenTodoIds = [];
    private readonly HashSet<string> _seenPipelineKeys = [];
    private readonly HttpClient _http;

    private GitlabConfig _config = new();
    private IPluginContext? _context;
    private bool _firstPoll = true;

    public string Id => "com.arrr.plugin.gitlab";
    public string Name => "GitLab";
    public string Version => VersionUtils.Get(typeof(GitlabSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls GitLab to-dos and CI pipeline statuses, publishing them to the event bus.";
    public string[] Categories => ["development", "git", "ci"];
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

        await PollTodosAsync(context, ct);
        await PollPipelinesAsync(context, ct);

        if (_firstPoll)
        {
            _firstPoll = false;
        }
    }

    private async Task PollTodosAsync(IPluginContext context, CancellationToken ct)
    {
        List<GitlabTodo>? todos;

        try
        {
            var baseUrl = _config.GitlabUrl.TrimEnd('/');
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v4/todos?state=pending");
            request.Headers.Add("PRIVATE-TOKEN", _config.PersonalAccessToken);

            var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _context?.Logger.LogWarning("GitLab todos API returned {Status}", response.StatusCode);

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
                _seenTodoIds.Add(id);

                continue;
            }

            if (_seenTodoIds.Add(id))
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
    }

    private async Task PollPipelinesAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.PipelineProjects.Count == 0)
        {
            return;
        }

        var baseUrl = _config.GitlabUrl.TrimEnd('/');

        foreach (var project in _config.PipelineProjects)
        {
            var encoded = Uri.EscapeDataString(project);
            List<GitlabPipeline>? pipelines;

            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/api/v4/projects/{encoded}/pipelines?per_page=10&order_by=updated_at&sort=desc"
                );
                request.Headers.Add("PRIVATE-TOKEN", _config.PersonalAccessToken);

                var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _context?.Logger.LogWarning(
                        "GitLab pipeline API returned {Status} for project {Project}",
                        response.StatusCode,
                        project
                    );

                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                pipelines = JsonSerializer.Deserialize<List<GitlabPipeline>>(json, JsonOptions);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _context?.Logger.LogError(ex, "GitLab pipeline fetch failed for project {Project}", project);

                continue;
            }

            if (pipelines is null)
            {
                continue;
            }

            foreach (var pipeline in pipelines)
            {
                var key = $"{project}:{pipeline.Id}:{pipeline.Status}";

                if (_firstPoll)
                {
                    _seenPipelineKeys.Add(key);

                    continue;
                }

                if (!TerminalStatuses.Contains(pipeline.Status))
                {
                    continue;
                }

                if (_seenPipelineKeys.Add(key))
                {
                    var emoji = pipeline.Status switch
                    {
                        "success"  => "✅",
                        "failed"   => "❌",
                        "canceled" => "⚠️",
                        _          => "🔔"
                    };

                    var priority = pipeline.Status == "failed"
                        ? NotificationPriority.High
                        : NotificationPriority.Normal;

                    await context.EventBus.PublishAsync(
                        new Notification(
                            Guid.NewGuid(),
                            Id,
                            $"{emoji} {project} [{pipeline.Ref}]",
                            $"Pipeline #{pipeline.Id} {pipeline.Status}",
                            DateTimeOffset.UtcNow,
                            null,
                            priority,
                            Url: pipeline.WebUrl,
                            Extras: new Dictionary<string, string>
                            {
                                ["gitlab.project"]     = project,
                                ["gitlab.pipeline_id"] = pipeline.Id.ToString(),
                                ["gitlab.ref"]         = pipeline.Ref,
                                ["gitlab.status"]      = pipeline.Status
                            }
                        ),
                        ct
                    );
                }
            }
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
                "GitLab plugin ready ({Url}), polling every {Interval} min — monitoring {Count} project(s) for pipelines",
                _config.GitlabUrl,
                _config.PollIntervalMinutes,
                _config.PipelineProjects.Count
            );
        }
    }
}
