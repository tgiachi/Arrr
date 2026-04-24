using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using TelegramPlugin.Data;
using TL;
using WTelegram;

namespace TelegramPlugin;

public class TelegramPlugin : ISourcePlugin, IConfigurablePlugin
{
    public string Id => "com.arrr.telegram";
    public string Name => "Telegram";
    public string Version => "1.0.0";
    public string Author => "Tom";
    public string Description => "Receives Telegram messages via MTProto user account and publishes notifications.";
    public string[] Categories => ["telegram", "messages"];
    public string Icon => "";
    public Type ConfigType => typeof(TelegramPluginConfig);

    public void Dispose() { }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        var config = await context.LoadConfigAsync<TelegramPluginConfig>(ct);

        if (config.ApiId == 0 || string.IsNullOrEmpty(config.ApiHash) || string.IsNullOrEmpty(config.PhoneNumber))
        {
            context.Logger.LogError("TelegramPlugin: ApiId, ApiHash and PhoneNumber are required. Configure the plugin and restart.");
            return;
        }

        var sessionPath = Path.ChangeExtension(context.ConfigPath, ".session");
        var codeFile = Path.ChangeExtension(context.ConfigPath, ".code");

        string ConfigCallback(string what) => what switch
        {
            "api_id" => config.ApiId.ToString(),
            "api_hash" => config.ApiHash,
            "phone_number" => config.PhoneNumber,
            "session_pathname" => sessionPath,
            "verification_code" => WaitForCode(codeFile, context.Logger),
            "password" => config.TwoFactorPassword,
            _ => ""
        };

        var filter = config.MonitoredChats
            .Select(c => c.Trim().ToLowerInvariant())
            .ToHashSet();

        using var client = new Client(ConfigCallback);

        var self = await client.LoginUserIfNeeded();
        context.Logger.LogInformation("Telegram logged in as {FirstName} {LastName} (@{Username})",
            self.first_name, self.last_name, self.username);

        client.OnUpdate += async updates =>
        {
            foreach (var update in updates.UpdateList)
            {
                if (update is not UpdateNewMessage unm) continue;
                if (unm.message is not Message msg) continue;
                if (msg.flags.HasFlag(Message.Flags.out_)) continue;

                var (senderName, chatName) = ResolvePeer(updates, msg);
                if (filter.Count > 0 && !filter.Contains(chatName.ToLowerInvariant())) continue;

                var title = msg.message.Length > 80 ? msg.message[..80] + "…" : msg.message;
                var body = string.IsNullOrEmpty(chatName) ? senderName : $"{senderName} → {chatName}";

                await context.EventBus.PublishAsync(
                    new Notification(
                        Guid.NewGuid(),
                        Id,
                        title,
                        body,
                        new DateTimeOffset(msg.date.ToUniversalTime()),
                        null
                    ),
                    ct
                );
            }
        };

        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private static (string sender, string chat) ResolvePeer(UpdatesBase updates, Message msg)
    {
        var sender = "";
        var chat = "";

        if (msg.from_id is PeerUser fromUser && updates.Users.TryGetValue(fromUser.user_id, out var fromUserObj))
            sender = $"{fromUserObj.first_name} {fromUserObj.last_name}".Trim();

        if (msg.peer_id is PeerUser pu && updates.Users.TryGetValue(pu.user_id, out var peerUser))
        {
            if (string.IsNullOrEmpty(sender))
                sender = $"{peerUser.first_name} {peerUser.last_name}".Trim();
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

    private static string GetChatTitle(ChatBase chatBase) => chatBase switch
    {
        Chat c => c.title,
        Channel ch => ch.title,
        ChatForbidden cf => cf.title,
        ChannelForbidden cf => cf.title,
        _ => ""
    };

    private static string WaitForCode(string codeFile, ILogger logger)
    {
        logger.LogWarning("Telegram verification code required. Write the code (digits only) to: {Path}", codeFile);
        File.WriteAllText(codeFile, "");

        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(2000);
            if (!File.Exists(codeFile)) continue;
            var code = File.ReadAllText(codeFile).Trim();
            if (string.IsNullOrEmpty(code)) continue;
            File.Delete(codeFile);
            return code;
        }

        throw new TimeoutException("Telegram verification code not provided within 10 minutes.");
    }
}
