using System.Collections.Concurrent;
using System.Text;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmtpSink.Data;
using SmtpSink.Internal;
using SmtpSink.Types;

namespace SmtpSink;

public class SmtpSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private readonly ConcurrentQueue<Notification> _digestQueue = new();
    private readonly IMailSender? _injectedSender;
    private readonly TimeSpan?    _testDigestInterval;

    private IMailSender?              _mailSender;
    private SmtpSinkConfig            _config = new();
    private ISinkContext?             _context;
    private CancellationTokenSource? _digestCts;
    private Task?                    _digestTask;

    public string Id          => "com.arrr.sink.smtp";
    public string Name        => "SMTP";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Delivers notifications via email using SMTP.";
    public string Icon        => "📧";
    public Type   ConfigType  => typeof(SmtpSinkConfig);

    public SmtpSinkPlugin() { }

    internal SmtpSinkPlugin(IMailSender sender, TimeSpan? digestInterval = null)
    {
        _injectedSender     = sender;
        _testDigestInterval = digestInterval;
    }

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context    = context;
        _config     = await context.LoadConfigAsync<SmtpSinkConfig>(ct);
        _mailSender = _injectedSender ?? new MailKitMailSender(_config);

        if (_config.Mode == SmtpDeliveryMode.Digest)
        {
            var interval = _testDigestInterval ?? TimeSpan.FromMinutes(_config.DigestIntervalMinutes);
            _digestCts  = new CancellationTokenSource();
            _digestTask = Task.Run(() => RunDigestLoopAsync(interval, _digestCts.Token));
        }

        context.Logger.LogInformation("SMTP sink ready — {Mode} mode to {To}", _config.Mode, _config.To);
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_mailSender is null || string.IsNullOrEmpty(_config.To))
            return;

        if (_config.Mode == SmtpDeliveryMode.Digest)
        {
            _digestQueue.Enqueue(notification);
            return;
        }

        var message = BuildSingleMessage(notification);
        try
        {
            await _mailSender.SendAsync(message, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "SMTP send failed for {Title}", notification.Title);
        }
    }

    private async Task RunDigestLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await FlushDigestAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }
    }

    private async Task FlushDigestAsync(CancellationToken ct)
    {
        if (_mailSender is null)
            return;

        var notifications = new List<Notification>();
        while (_digestQueue.TryDequeue(out var n))
            notifications.Add(n);

        if (notifications.Count == 0)
            return;

        var message = BuildDigestMessage(notifications);
        try
        {
            await _mailSender.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "SMTP digest send failed ({Count} notifications)", notifications.Count);
        }
    }

    private MimeMessage BuildSingleMessage(Notification notification)
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_config.From));
        msg.To.Add(MailboxAddress.Parse(_config.To));
        msg.Subject = ApplyTemplate(_config.SubjectTemplate, notification);
        msg.Body    = new TextPart("plain") { Text = ApplyTemplate(_config.BodyTemplate, notification) };
        return msg;
    }

    private MimeMessage BuildDigestMessage(IReadOnlyList<Notification> notifications)
    {
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_config.From));
        msg.To.Add(MailboxAddress.Parse(_config.To));
        msg.Subject = $"[Arrr] Digest — {notifications.Count} notifications";

        var sb = new StringBuilder();
        foreach (var n in notifications)
        {
            sb.AppendLine($"[{n.Source}] {n.Title}");
            sb.AppendLine(n.Body);
            sb.AppendLine();
        }

        msg.Body = new TextPart("plain") { Text = sb.ToString() };
        return msg;
    }

    private static string ApplyTemplate(string template, Notification n) =>
        template
            .Replace("{source}", n.Source)
            .Replace("{title}",  n.Title)
            .Replace("{body}",   n.Body);

    public async Task StopAsync()
    {
        if (_digestCts is not null)
        {
            _digestCts.Cancel();
            if (_digestTask is not null)
            {
                try
                {
                    await _digestTask.ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _context?.Logger.LogError(ex, "Digest loop exited with error");
                }
            }
            await FlushDigestAsync(CancellationToken.None);
            _digestCts.Dispose();
            _digestCts = null;
        }

        _mailSender = null;
    }
}
