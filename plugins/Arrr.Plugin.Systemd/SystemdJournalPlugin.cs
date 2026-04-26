using System.Diagnostics;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Arrr.Plugin.Systemd.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Plugin.Systemd;

public class SystemdJournalPlugin : ISourcePlugin, IConfigurablePlugin
{
    private readonly Func<SystemdConfig, CancellationToken, IAsyncEnumerable<string>>? _lineReaderOverride;

    private SystemdConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.systemd";
    public string Name => "systemd Journal";
    public string Version => VersionUtils.Get(typeof(SystemdJournalPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Tails the systemd journal and publishes log events as notifications.";
    public string[] Categories => ["system", "linux"];
    public string Icon => "🐧";
    public Type ConfigType => typeof(SystemdConfig);

    public SystemdJournalPlugin() { }

    internal SystemdJournalPlugin(Func<SystemdConfig, CancellationToken, IAsyncEnumerable<string>> lineReader)
    {
        _lineReaderOverride = lineReader;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<SystemdConfig>(ct);

        context.Logger.LogInformation(
            "systemd Journal plugin ready, capturing priority <= {Priority}, units: {Units}",
            _config.MinPriority,
            _config.Units.Count > 0 ? string.Join(", ", _config.Units) : "all"
        );

        var reader = _lineReaderOverride ?? ReadJournalLines;

        await foreach (var line in reader(_config, ct))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JournalEntry? entry;

            try
            {
                entry = JsonSerializer.Deserialize<JournalEntry>(line);
            }
            catch
            {
                continue;
            }

            if (entry is null || string.IsNullOrEmpty(entry.Message))
            {
                continue;
            }

            var unit = entry.SystemdUnit ?? entry.SyslogIdentifier ?? "journal";
            var body = _config.MaxMessageLength > 0 && entry.Message.Length > _config.MaxMessageLength
                ? entry.Message[.._config.MaxMessageLength] + "…"
                : entry.Message;

            var priorityLabel = entry.Priority switch
            {
                "0" => "EMERG",
                "1" => "ALERT",
                "2" => "CRIT",
                "3" => "ERR",
                "4" => "WARN",
                "5" => "NOTICE",
                "6" => "INFO",
                _ => "DEBUG"
            };

            await context.EventBus.PublishAsync(
                new Notification(
                    Guid.NewGuid(),
                    Id,
                    $"[{priorityLabel}] {unit}",
                    body,
                    DateTimeOffset.UtcNow,
                    null
                ),
                ct
            );
        }
    }

    private static async IAsyncEnumerable<string> ReadJournalLines(
        SystemdConfig config,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var args = BuildJournalctlArgs(config);

        var psi = new ProcessStartInfo(config.JournalctlPath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start journalctl");

        while (!ct.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(ct);

            if (line is null)
            {
                break;
            }

            yield return line;
        }

        if (!process.HasExited)
        {
            process.Kill();
        }
    }

    private static string BuildJournalctlArgs(SystemdConfig config)
    {
        var parts = new List<string> { "-f", "-o", "json", "--no-pager", $"-p {config.MinPriority}" };

        foreach (var unit in config.Units)
        {
            parts.Add($"-u {unit}");
        }

        return string.Join(" ", parts);
    }
}
