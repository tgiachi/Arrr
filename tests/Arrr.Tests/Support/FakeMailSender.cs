using MimeKit;
using SmtpSink.Internal;

namespace Arrr.Tests.Support;

internal class FakeMailSender : IMailSender
{
    public List<MimeMessage> Sent { get; } = [];

    public Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
