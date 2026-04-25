using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SmtpSink.Data;

namespace SmtpSink.Internal;

internal class MailKitMailSender : IMailSender
{
    private readonly SmtpSinkConfig _config;

    public MailKitMailSender(SmtpSinkConfig config)
    {
        _config = config;
    }

    public async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();
        var socketOptions = _config.UseSsl
                                ? SecureSocketOptions.StartTls
                                : SecureSocketOptions.None;

        await client.ConnectAsync(_config.Host, _config.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(_config.Username))
        {
            await client.AuthenticateAsync(_config.Username, _config.Password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
