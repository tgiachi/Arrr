using Arrr.Core.Data.Api;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using ImapPlugin.Data;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace ImapPlugin;

public class ImapPlugin : IPollingPlugin, IConfigurablePlugin, ITestablePlugin
{
    private readonly HashSet<string> _seenIds = [];

    private ImapPluginConfig _config = new();
    private bool _firstPoll = true;

    public string Id => "com.arrr.imap";
    public string Name => "IMAP";
    public string Version => VersionUtils.Get(typeof(ImapPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Polls one or more IMAP mailboxes and publishes notifications for new messages.";
    public string[] Categories => ["email", "imap"];
    public string Icon => "";
    public Type ConfigType => typeof(ImapPluginConfig);

    public TimeSpan Interval => TimeSpan.FromMinutes(_config.PollIntervalMinutes > 0 ? _config.PollIntervalMinutes : 2);

    public async Task PollAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Accounts.Count == 0)
        {
            return;
        }

        foreach (var account in _config.Accounts)
        {
            if (string.IsNullOrEmpty(account.Host))
            {
                continue;
            }

            await PollAccountAsync(account, context, ct);
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
            "IMAP plugin configured with {Count} account(s)",
            _config.Accounts.Count
        );
    }

    public async Task<PluginTestResult> TestAsync(IPluginContext context, CancellationToken ct)
    {
        if (_config.Accounts.Count == 0)
        {
            return new(false, "No accounts configured.");
        }

        var lines = new List<string>();
        var allOk = true;

        foreach (var account in _config.Accounts)
        {
            if (string.IsNullOrEmpty(account.Host))
            {
                lines.Add($"{account.Name}: ⚠ no host configured");
                allOk = false;
                continue;
            }

            using var client = new ImapClient();

            try
            {
                await client.ConnectAsync(account.Host, account.Port, account.UseSsl, ct);
                await client.AuthenticateAsync(account.Username, account.Password, ct);
                await client.DisconnectAsync(true, ct);

                var label = string.IsNullOrEmpty(account.Name) ? account.Username : account.Name;
                lines.Add($"{label}: ✓ OK ({account.Host}:{account.Port})");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var label = string.IsNullOrEmpty(account.Name) ? account.Username : account.Name;
                lines.Add($"{label}: ✗ {ex.Message}");
                allOk = false;
            }
        }

        return new(allOk, string.Join("\n", lines));
    }

    private async Task PollAccountAsync(ImapAccountConfig account, IPluginContext context, CancellationToken ct)
    {
        using var client = new ImapClient();

        try
        {
            await client.ConnectAsync(account.Host, account.Port, account.UseSsl, ct);
            await client.AuthenticateAsync(account.Username, account.Password, ct);

            var folder = await client.GetFolderAsync(account.Folder, ct);
            await folder.OpenAsync(FolderAccess.ReadOnly, ct);

            var uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);

            foreach (var uid in uids)
            {
                var key = $"{account.Host}:{account.Folder}:{uid}";

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
                var accountLabel = string.IsNullOrEmpty(account.Name) ? account.Username : account.Name;

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        Id,
                        subject,
                        $"From: {from} [{accountLabel}]",
                        message.Date == default ? DateTimeOffset.UtcNow : message.Date,
                        null,
                        Extras: new Dictionary<string, string>
                        {
                            ["imap.sender"] = from,
                            ["imap.folder"] = account.Folder,
                            ["imap.account"] = accountLabel
                        }
                    ),
                    ct
                );
            }

            await folder.CloseAsync(false, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "IMAP poll failed for account {Account}", account.Username);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, ct);
            }
        }
    }
}
