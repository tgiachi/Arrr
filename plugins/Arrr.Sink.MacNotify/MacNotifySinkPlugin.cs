using System.Diagnostics;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Types;
using Arrr.Core.Utils;
using Arrr.Sink.MacNotify.Data;
using Microsoft.Extensions.Logging;

namespace Arrr.Sink.MacNotify;

public class MacNotifySinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private MacNotifyConfig _config = new();
    private ISinkContext? _context;
    private bool _initialized;

    public string Id => "com.arrr.sink.mac-notify";
    public string Name => "macOS Notifications";
    public string Version => VersionUtils.Get(typeof(MacNotifySinkPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Delivers notifications as native macOS notifications via osascript.";
    public string Icon => "🍎";
    public Type ConfigType => typeof(MacNotifyConfig);
    public PlatformType[] Platforms => [PlatformType.Osx];

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (!_initialized)
        {
            return;
        }

        try
        {
            var script = BuildScript(notification);
            using var proc = new Process();
            proc.StartInfo = new("osascript", ["-e", script])
            {
                RedirectStandardError = true,
                UseShellExecute = false
            };
            proc.Start();
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                _context?.Logger.LogWarning("osascript exited {Code}: {Error}", proc.ExitCode, err);
            }
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "macOS notification failed: {Title}", notification.Title);
        }
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<MacNotifyConfig>(ct);
        _initialized = OperatingSystem.IsMacOS();

        if (_initialized)
        {
            context.Logger.LogInformation("macOS notification sink ready");
        }
        else
        {
            context.Logger.LogWarning("macOS notification sink unavailable on this platform");
        }
    }

    public Task StopAsync()
    {
        _initialized = false;

        return Task.CompletedTask;
    }

    private string BuildScript(Notification notification)
    {
        var title = Escape(notification.Title);
        var body = Escape(notification.Body);
        var script = $"display notification \"{body}\" with title \"{title}\"";

        if (_config.ShowSource && !string.IsNullOrWhiteSpace(notification.Source))
        {
            script += $" subtitle \"{Escape(notification.Source)}\"";
        }

        if (!string.IsNullOrEmpty(_config.Sound))
        {
            script += $" sound name \"{Escape(_config.Sound)}\"";
        }

        return script;
    }

    private static string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
