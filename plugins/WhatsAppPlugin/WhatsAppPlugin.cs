using System.Diagnostics;
using System.Text.Json;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using QRCoder;
using WhatsAppPlugin.Data;

namespace WhatsAppPlugin;

public class WhatsAppPlugin : ISourcePlugin, IConfigurablePlugin
{
    public string Id => "com.arrr.whatsapp";
    public string Name => "WhatsApp";
    public string Version => "1.0.0";
    public string Author => "Tom";
    public string Description => "Receives WhatsApp messages via whatsapp-bridge (whatsmeow) and publishes notifications.";
    public string[] Categories => ["whatsapp", "messages"];
    public string Icon => "";
    public Type ConfigType => typeof(WhatsAppPluginConfig);

    public void Dispose() { }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        var config = await context.LoadConfigAsync<WhatsAppPluginConfig>(ct);

        if (string.IsNullOrEmpty(config.BridgePath) || !File.Exists(config.BridgePath))
        {
            context.Logger.LogError(
                "whatsapp-bridge binary not found at '{Path}'. Build it: cd plugins/WhatsAppPlugin/bridge && ./build.sh",
                config.BridgePath);
            return;
        }

        var sessionPath = Path.ChangeExtension(context.ConfigPath, ".db");
        var filter = config.MonitoredChats
            .Select(c => c.Trim().ToLowerInvariant())
            .ToHashSet();

        var psi = new ProcessStartInfo(config.BridgePath, sessionPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = psi };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                context.Logger.LogDebug("[bridge] {Line}", e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();

        await using (ct.Register(() => { if (!process.HasExited) process.Kill(); }))
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null) break;
                HandleLine(line, context, filter, ct);
            }
        }

        await process.WaitForExitAsync(CancellationToken.None);
    }

    private static void HandleLine(string line, IPluginContext context, HashSet<string> filter, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            switch (root.GetProperty("type").GetString())
            {
                case "qr":
                    RenderQr(root.GetProperty("code").GetString() ?? "", context.Logger);
                    break;

                case "ready":
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
                        if (!filter.Contains(key)) return;
                    }

                    var timestamp = tsStr is not null && DateTimeOffset.TryParse(tsStr, out var dto)
                        ? dto
                        : DateTimeOffset.UtcNow;

                    var title = body.Length > 80 ? body[..80] + "…" : body;
                    var subtitle = string.IsNullOrEmpty(chat) ? from : $"{from} → {chat}";

                    _ = context.EventBus.PublishAsync(
                        new Notification(Guid.NewGuid(), "com.arrr.whatsapp", title, subtitle, timestamp, null),
                        ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogDebug(ex, "Could not parse bridge line: {Line}", line);
        }
    }

    private static void RenderQr(string code, ILogger logger)
    {
        try
        {
            var data = new QRCodeGenerator().CreateQrCode(code, QRCodeGenerator.ECCLevel.L);
            var art = new AsciiQRCode(data).GetGraphic(1);
            logger.LogWarning(
                "Open WhatsApp → Settings → Linked Devices → Link a Device and scan:\n{QR}",
                art);
        }
        catch
        {
            logger.LogWarning("WhatsApp QR code data (render with any QR tool): {Code}", code);
        }
    }
}
