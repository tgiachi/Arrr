using System.ComponentModel;
using Arrr.Core.Attributes;
using SmtpSink.Types;

namespace SmtpSink.Data;

public class SmtpSinkConfig
{
    [Description("SMTP server hostname")]
    public string Host { get; set; } = "";

    [Description("SMTP server port")]
    public int Port { get; set; } = 587;

    [Description("SMTP username")]
    public string Username { get; set; } = "";

    [Description("SMTP password")]
    [Sensitive]
    public string Password { get; set; } = "";

    [Description("Use STARTTLS/SSL")]
    public bool UseSsl { get; set; } = true;

    [Description("Sender email address")]
    public string From { get; set; } = "";

    [Description("Recipient email address")]
    public string To { get; set; } = "";

    [Description("Subject template — {source}, {title}, {body} placeholders")]
    public string SubjectTemplate { get; set; } = "[Arrr] [{source}] {title}";

    [Description("Body template — {source}, {title}, {body} placeholders")]
    public string BodyTemplate { get; set; } = "{title}\n\n{body}";

    [Description("Delivery mode: Single or Digest")]
    public SmtpDeliveryMode Mode { get; set; } = SmtpDeliveryMode.Single;

    [Description("Digest flush interval in minutes (Digest mode only)")]
    public int DigestIntervalMinutes { get; set; } = 60;
}
