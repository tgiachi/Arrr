using System.Net;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Arrr.Plugin.Todoist.Data;
using Arrr.Plugin.Todoist.Data.Internal;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Todoist;

public class TodoistSourcePlugin : IPollingPlugin, IConfigurablePlugin, IDisposable
{
    private readonly HashSet<string> _notifiedKeys = [];
    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    private TodoistConfig _config = new();
    private IPluginContext? _context;
    private DateTimeOffset _lastPollTime;
    private bool _firstPoll = true;
    private bool _remindersAvailable = true;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string Id => "com.arrr.plugin.todoist";
    public string Name => "Todoist";
    public string Version => VersionUtils.Get(typeof(TodoistSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls Todoist tasks and publishes notifications for upcoming due dates and reminders.";
    public string[] Categories => ["productivity", "tasks"];
    public string Icon => "✅";
    public Type ConfigType => typeof(TodoistConfig);
    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes);

    public TodoistSourcePlugin()
    {
        _httpClient = new();
        _timeProvider = TimeProvider.System;
    }

    internal TodoistSourcePlugin(HttpMessageHandler handler, TimeProvider timeProvider)
    {
        _httpClient = new(handler);
        _timeProvider = timeProvider;
    }

    public void Dispose()
        => _httpClient.Dispose();

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.ApiToken))
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var tasks = await FetchTasksAsync(ct);

        foreach (var task in tasks)
        {
            if (task.Due?.Datetime is null)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(task.Due.Datetime, out var dueTime))
            {
                continue;
            }

            foreach (var alertMin in _config.AlertMinutes)
            {
                var triggerTime = dueTime - TimeSpan.FromMinutes(alertMin);
                var key = $"{task.Id}_{alertMin}";

                if (_firstPoll)
                {
                    if (triggerTime <= now)
                    {
                        _notifiedKeys.Add(key);
                    }

                    continue;
                }

                if (triggerTime > _lastPollTime && triggerTime <= now && _notifiedKeys.Add(key))
                {
                    await context.EventBus.PublishAsync(BuildDueDateNotification(task, alertMin, now), ct);
                }
            }
        }

        if (_config.NotifyReminders && _remindersAvailable)
        {
            await ProcessRemindersAsync(context, tasks, now, ct);
        }

        _lastPollTime = now;
        _firstPoll = false;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<TodoistConfig>(ct);
        context.Logger.LogInformation("Todoist plugin loaded, filter: {Filter}", _config.Filter);
    }

    private static Notification BuildDueDateNotification(TodoistTaskResponse task, int alertMin, DateTimeOffset now)
    {
        var body = alertMin > 0 ? $"In {alertMin} min" : "Due now";

        return new(
            Guid.NewGuid(),
            "com.arrr.plugin.todoist",
            $"✅ {task.Content}",
            body,
            now,
            null,
            MapPriority(task.Priority),
            Extras: new Dictionary<string, string>
            {
                ["todoist.task_id"] = task.Id,
                ["todoist.project_id"] = task.ProjectId,
                ["todoist.priority"] = task.Priority.ToString(),
                ["todoist.due_datetime"] = task.Due?.Datetime ?? ""
            }
        );
    }

    private async Task<List<TodoistReminderResponse>> FetchRemindersAsync(CancellationToken ct)
    {
        const string url = "https://api.todoist.com/rest/v2/reminders";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", _config.ApiToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        return JsonSerializer.Deserialize<List<TodoistReminderResponse>>(json, JsonOptions) ?? [];
    }

    private async Task<List<TodoistTaskResponse>> FetchTasksAsync(CancellationToken ct)
    {
        var encoded = Uri.EscapeDataString(_config.Filter);
        var url = $"https://api.todoist.com/rest/v2/tasks?filter={encoded}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", _config.ApiToken);

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Deserialize<List<TodoistTaskResponse>>(json, JsonOptions) ?? [];
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Todoist: failed to fetch tasks");

            return [];
        }
    }

    private static NotificationPriority MapPriority(int todoistPriority)
        => todoistPriority switch
        {
            4 => NotificationPriority.Critical,
            3 => NotificationPriority.High,
            _ => NotificationPriority.Normal
        };

    private async Task ProcessRemindersAsync(
        IPluginContext context,
        IReadOnlyList<TodoistTaskResponse> tasks,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        List<TodoistReminderResponse> reminders;

        try
        {
            reminders = await FetchRemindersAsync(ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            _context?.Logger.LogWarning("Reminders require Todoist Pro — disabling reminder fetching");
            _remindersAvailable = false;

            return;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Todoist: failed to fetch reminders");

            return;
        }

        var taskMap = tasks.ToDictionary(t => t.Id);

        foreach (var reminder in reminders)
        {
            if (reminder.Due.Datetime is null)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(reminder.Due.Datetime, out var reminderTime))
            {
                continue;
            }

            var key = $"reminder_{reminder.Id}";

            if (_firstPoll)
            {
                if (reminderTime <= now)
                {
                    _notifiedKeys.Add(key);
                }

                continue;
            }

            if (reminderTime > _lastPollTime && reminderTime <= now && _notifiedKeys.Add(key))
            {
                taskMap.TryGetValue(reminder.TaskId, out var task);
                var title = task is not null ? $"⏰ {task.Content}" : "⏰ Reminder";

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        "com.arrr.plugin.todoist",
                        title,
                        "Reminder",
                        now,
                        null,
                        Extras: new Dictionary<string, string>
                        {
                            ["todoist.task_id"] = reminder.TaskId,
                            ["todoist.reminder_id"] = reminder.Id
                        }
                    ),
                    ct
                );
            }
        }
    }
}
