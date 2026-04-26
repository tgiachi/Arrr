using MimeKit;

namespace SmtpSink.Internal;

internal interface IMailSender
{
    Task SendAsync(MimeMessage message, CancellationToken ct);
}
