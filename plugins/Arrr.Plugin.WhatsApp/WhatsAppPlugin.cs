using System.Diagnostics;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using WhatsAppPlugin.Data;

namespace WhatsAppPlugin;

public class WhatsAppPlugin : ISourcePlugin, IConfigurablePlugin, IQrPlugin
{
    public string Id => "com.arrr.whatsapp";
    public string Name => "WhatsApp";
    public string Version => VersionUtils.Get(typeof(WhatsAppPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Receives WhatsApp messages via whatsapp-bridge (whatsmeow) and publishes notifications.";
    public string[] Categories => ["whatsapp", "messages"];
    public string Icon => "";
    public Type ConfigType => typeof(WhatsAppPluginConfig);

    public string? PendingQrCode { get; private set; }

    public void Dispose() { }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        var config = await context.LoadConfigAsync<WhatsAppPluginConfig>(ct);

        var bridgePath = config.BridgePath;

        if (!Path.IsPathRooted(bridgePath))
        {
            var pluginDir = Path.GetDirectoryName(typeof(WhatsAppPlugin).Assembly.Location) ?? "";
            bridgePath = Path.Combine(pluginDir, bridgePath);

            if (!File.Exists(bridgePath) && OperatingSystem.IsWindows())
            {
                bridgePath += ".exe";
            }
        }

        if (!File.Exists(bridgePath))
        {
            context.Logger.LogError(
                "whatsapp-bridge binary not found at '{Path}'. Install Arrr.Plugin.WhatsApp from NuGet or build the bridge manually.",
                bridgePath
            );

            return;
        }

        var sessionPath = Path.ChangeExtension(context.ConfigPath, ".db");
        var filter = config.MonitoredChats
                           .Select(c => c.Trim().ToLowerInvariant())
                           .ToHashSet();

        var psi = new ProcessStartInfo(bridgePath, sessionPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
                                     {
                                         if (e.Data is not null)
                                         {
                                             context.Logger.LogDebug("[bridge] {Line}", e.Data);
                                         }
                                     };

        process.Start();
        process.BeginErrorReadLine();

        await using (ct.Register(
                         () =>
                         {
                             if (!process.HasExited)
                             {
                                 process.Kill();
                             }
                         }
                     ))
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);

                if (line is null)
                {
                    break;
                }
                HandleLine(line, context, filter, ct);
            }
        }

        PendingQrCode = null;
        await process.WaitForExitAsync(CancellationToken.None);
    }

    private void HandleLine(string line, IPluginContext context, HashSet<string> filter, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            switch (root.GetProperty("type").GetString())
            {
                case "qr":
                    PendingQrCode = root.GetProperty("code").GetString();
                    context.Logger.LogInformation("WhatsApp QR code ready — open the UI to scan it.");

                    break;

                case "ready":
                    PendingQrCode = null;
                    var name = root.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var jid = root.TryGetProperty("jid", out var j) ? j.GetString() : "";
                    context.Logger.LogInformation("WhatsApp connected as {Name} ({JID})", name, jid);

                    break;

                case "message":
                    var from = root.TryGetProperty("from", out var f) ? f.GetString() ?? "" : "";
                    var chat = root.TryGetProperty("chat", out var ch) ? ch.GetString() ?? "" : "";
                    var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    var tsStr = root.TryGetProperty("ts", out var t) ? t.GetString() : null;

                    if (filter.Count > 0)
                    {
                        var key = (string.IsNullOrEmpty(chat) ? from : chat).ToLowerInvariant();

                        if (!filter.Contains(key))
                        {
                            return;
                        }
                    }

                    var timestamp = tsStr is not null && DateTimeOffset.TryParse(tsStr, out var dto)
                                        ? dto
                                        : DateTimeOffset.UtcNow;

                    var title = body.Length > 80 ? body[..80] + "…" : body;
                    var subtitle = string.IsNullOrEmpty(chat) ? from : $"{from} → {chat}";

                    _ = context.EventBus.PublishAsync(
                        new Notification(
                            Guid.NewGuid(),
                            Id,
                            title,
                            subtitle,
                            timestamp,
                            null,
                            Extras: new Dictionary<string, string>
                            {
                                ["whatsapp.from"] = from,
                                ["whatsapp.chat"] = chat,
                            }
                        ),
                        ct
                    );

                    break;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogDebug(ex, "Could not parse bridge line: {Line}", line);
        }
    }
}
