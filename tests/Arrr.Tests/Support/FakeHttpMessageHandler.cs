using System.Net;

namespace Arrr.Tests.Support;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseContent)
        });
    }
}
