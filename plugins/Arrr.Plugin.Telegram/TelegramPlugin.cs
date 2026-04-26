using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using TelegramPlugin.Data;
using TL;
using WTelegram;

namespace TelegramPlugin;

public class TelegramPlugin : ISourcePlugin, IConfigurablePlugin, ICallbackPlugin
{
    public string Id => "com.arrr.telegram";
    public string Name => "Telegram";
    public string Version => VersionUtils.Get(typeof(TelegramPlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Receives Telegram messages via MTProto user account and publishes notifications.";
    public string[] Categories => ["telegram", "messages"];
    public string Icon => "";
    public Type ConfigType => typeof(TelegramPluginConfig);

    private TaskCompletionSource<string>? _pendingCode;

    public void Dispose() { }

    public Task HandleCallbackAsync(string body, CancellationToken ct)
    {
        var code = body.Trim();

        if (_pendingCode is not null && !_pendingCode.Task.IsCompleted)
        {
            _pendingCode.TrySetResult(code);
        }

        return Task.CompletedTask;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        var config = await context.LoadConfigAsync<TelegramPluginConfig>(ct);

        if (config.ApiId == 0 || string.IsNullOrEmpty(config.ApiHash) || string.IsNullOrEmpty(config.PhoneNumber))
        {
            context.Logger.LogError(
                "TelegramPlugin: ApiId, ApiHash and PhoneNumber are required. Configure the plugin and restart."
            );

            return;
        }

        var sessionPath = Path.ChangeExtension(context.ConfigPath, ".session");

        string ConfigCallback(string what)
            => what switch
            {
                "api_id"            => config.ApiId.ToString(),
                "api_hash"          => config.ApiHash,
                "phone_number"      => config.PhoneNumber,
                "session_pathname"  => sessionPath,
                "verification_code" => WaitForCode(context.Logger, ct),
                "password"          => string.IsNullOrEmpty(config.TwoFactorPassword) ? null : config.TwoFactorPassword,
                _                   => null
            };

        var filter = config.MonitoredChats
                           .Select(c => c.Trim().ToLowerInvariant())
                           .ToHashSet();

        Helpers.Log = (level, message) => context.Logger.LogDebug("[WTelegram] {Message}", message);

        using var client = new Client(ConfigCallback);

        var self = await client.LoginUserIfNeeded();
        context.Logger.LogInformation(
            "Telegram logged in as {FirstName} {LastName} (@{Username})",
            self.first_name,
            self.last_name,
            self.username
        );

        client.OnUpdates += async updates =>
                            {
                                foreach (var update in updates.UpdateList)
                                {
                                    if (update is not UpdateNewMessage unm)
                                    {
                                        continue;
                                    }

                                    if (unm.message is not Message msg)
                                    {
                                        continue;
                                    }

                                    if (msg.flags.HasFlag(Message.Flags.out_))
                                    {
                                        continue;
                                    }

                                    var (senderName, chatName) = ResolvePeer(updates, msg);

                                    if (filter.Count > 0 && !filter.Contains(chatName.ToLowerInvariant()))
                                    {
                                        continue;
                                    }

                                    var title = msg.message.Length > 80 ? msg.message[..80] + "…" : msg.message;
                                    var body = string.IsNullOrEmpty(chatName) ? senderName : $"{senderName} → {chatName}";

                                    await context.EventBus.PublishAsync(
                                        new Notification(
                                            Guid.NewGuid(),
                                            Id,
                                            title,
                                            body,
                                            new(msg.date.ToUniversalTime()),
                                            null,
                                            Extras: new Dictionary<string, string>
                                            {
                                                ["telegram.sender"] = senderName,
                                                ["telegram.chat"] = chatName,
                                            }
                                        ),
                                        ct
                                    );
                                }
                            };

        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private static string GetChatTitle(ChatBase chatBase)
        => chatBase switch
        {
            Chat c              => c.title,
            Channel ch          => ch.title,
            ChatForbidden cf    => cf.title,
            ChannelForbidden cf => cf.title,
            _                   => ""
        };

    private static (string sender, string chat) ResolvePeer(UpdatesBase updates, Message msg)
    {
        var sender = "";
        var chat = "";

        if (msg.from_id is PeerUser fromUser && updates.Users.TryGetValue(fromUser.user_id, out var fromUserObj))
        {
            sender = $"{fromUserObj.first_name} {fromUserObj.last_name}".Trim();
        }

        if (msg.peer_id is PeerUser pu && updates.Users.TryGetValue(pu.user_id, out var peerUser))
        {
            if (string.IsNullOrEmpty(sender))
            {
                sender = $"{peerUser.first_name} {peerUser.last_name}".Trim();
            }
        }
        else if (msg.peer_id is PeerChat pc && updates.Chats.TryGetValue(pc.chat_id, out var grp))
        {
            chat = GetChatTitle(grp);
        }
        else if (msg.peer_id is PeerChannel pch && updates.Chats.TryGetValue(pch.channel_id, out var ch))
        {
            chat = GetChatTitle(ch);
        }

        return (sender, chat);
    }

    private string WaitForCode(ILogger logger, CancellationToken ct)
    {
        _pendingCode = new(TaskCreationOptions.RunContinuationsAsynchronously);

        logger.LogWarning(
            "Telegram verification code required. Send it via POST /api/plugins/{Id}/callback with the code as plain text body.",
            Id
        );

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(10));

        try
        {
            return _pendingCode.Task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Telegram verification code not provided within 10 minutes.");
        }
        finally
        {
            _pendingCode = null;
        }
    }
}
