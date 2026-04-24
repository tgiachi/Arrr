using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramBotSink.Data;

namespace TelegramBotSink;

public class TelegramBotSinkPlugin : ISinkPlugin, IConfigurablePlugin
{
    private TelegramBotClient? _bot;
    private TelegramBotSinkConfig _config = new();
    private ISinkContext? _context;

    public string Id          => "com.arrr.sink.telegram-bot";
    public string Name        => "Telegram Bot";
    public string Version     => "1.0.0";
    public string Author      => "Arrr";
    public string Description => "Delivers notifications to a Telegram chat or group via Bot API.";
    public string Icon        => "✈️";
    public Type   ConfigType  => typeof(TelegramBotSinkConfig);

    public async Task StartAsync(ISinkContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<TelegramBotSinkConfig>(ct);

        if (!string.IsNullOrWhiteSpace(_config.BotToken))
        {
            _bot = new TelegramBotClient(_config.BotToken);
            context.Logger.LogInformation("Telegram Bot sink ready — chat {ChatId}", _config.ChatId);
        }
        else
        {
            context.Logger.LogWarning("Telegram Bot sink: BotToken not configured, messages will be skipped");
        }
    }

    public async Task ConsumeAsync(Notification notification, CancellationToken ct)
    {
        if (_bot is null || string.IsNullOrWhiteSpace(_config.ChatId))
            return;

        var text = _config.Template
            .Replace("{source}", EscapeHtml(notification.Source))
            .Replace("{title}", EscapeHtml(notification.Title))
            .Replace("{body}", EscapeHtml(notification.Body));

        try
        {
            await _bot.SendMessage(
                chatId: _config.ChatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            _context?.Logger.LogError(ex, "Failed to send Telegram message for notification {Title}", notification.Title);
        }
    }

    public Task StopAsync()
    {
        _bot = null;
        return Task.CompletedTask;
    }

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
