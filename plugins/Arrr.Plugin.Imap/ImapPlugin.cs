using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using ImapPlugin.Data;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace ImapPlugin;

public class ImapPlugin : IPollingPlugin, IConfigurablePlugin
{
    private readonly HashSet<string> _seenIds = [];

    private ImapPluginConfig _config = new();
    private bool _firstPoll = true;

    public string Id => "com.arrr.imap";
    public string Name => "IMAP";
    public string Version => VersionUtils.Get(typeof(ImapPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls an IMAP mailbox and publishes notifications for new messages.";
    public string[] Categories => ["email", "imap"];
    public string Icon => "";
    public Type ConfigType => typeof(ImapPluginConfig);

    public TimeSpan Interval => TimeSpan.FromMinutes(2);

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.Host))
        {
            return;
        }

        using var client = new ImapClient();

        try
        {
            await client.ConnectAsync(_config.Host, _config.Port, _config.UseSsl, ct);
            await client.AuthenticateAsync(_config.Username, _config.Password, ct);

            var folder = await client.GetFolderAsync(_config.Folder, ct);
            await folder.OpenAsync(FolderAccess.ReadOnly, ct);

            var uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);

            foreach (var uid in uids)
            {
                var key = $"{_config.Host}:{_config.Folder}:{uid}";

                if (!_seenIds.Add(key))
                {
                    continue;
                }

                if (_firstPoll)
                {
                    continue;
                }

                var message = await folder.GetMessageAsync(uid, ct);
                var from = message.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown";
                var subject = message.Subject ?? "(no subject)";

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        Id,
                        subject,
                        $"From: {from}",
                        message.Date == default ? DateTimeOffset.UtcNow : message.Date,
                        null,
                        Extras: new Dictionary<string, string>
                        {
                            ["imap.sender"] = from,
                            ["imap.folder"] = _config.Folder
                        }
                    ),
                    ct
                );
            }

            await folder.CloseAsync(false, ct);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, ct);
            }
        }

        if (_firstPoll)
        {
            context.Logger.LogInformation("IMAP first poll complete — {Count} unseen message(s) indexed", _seenIds.Count);
            _firstPoll = false;
        }
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _config = await context.LoadConfigAsync<ImapPluginConfig>(ct);
        context.Logger.LogInformation(
            "IMAP plugin configured for {User}@{Host}:{Port} folder={Folder}",
            _config.Username,
            _config.Host,
            _config.Port,
            _config.Folder
        );
    }
}
