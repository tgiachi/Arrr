using System.Net;

namespace Arrr.Tests.Support;

internal class FakeHttpMessageHandler : HttpMessageHandler, IDisposable
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "";
    public bool ShouldTimeout { get; set; } = false;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;

        if (ShouldTimeout)
            throw new TaskCanceledException("Simulated timeout");

        return Task.FromResult(
            new HttpResponseMessage(ResponseStatusCode)
            {
                Content = new StringContent(ResponseContent)
            }
        );
    }
}
