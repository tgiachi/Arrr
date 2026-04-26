using System.ComponentModel;
using Arrr.Core.Attributes;

namespace TelegramBotSink.Data;

public class TelegramBotSinkConfig
{
    [Description("Bot token from @BotFather (e.g. 123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11)")]
    [Sensitive]
    public string BotToken { get; set; } = "";

    [Description("Target chat ID, group ID or @channelname")]
    public string ChatId { get; set; } = "";

    [Description("Message template — {source}, {title} and {body} are replaced at runtime")]
    public string Template { get; set; } = "<b>[{source}] {title}</b>\n{body}";
}
