using System.Net;

namespace Arrr.Tests.Support;

internal class FakeHttpMessageHandler : HttpMessageHandler, IDisposable
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = [];
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "";
    public bool ShouldTimeout { get; set; } = false;

    // URL substring → response content; takes priority over ResponseContent when matched.
    public Dictionary<string, string> ResponsesByUrl { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        Requests.Add(request);

        if (ShouldTimeout)
        {
            throw new TaskCanceledException("Simulated timeout");
        }

        var url = request.RequestUri?.ToString() ?? "";
        var content = ResponsesByUrl.FirstOrDefault(kv => url.Contains(kv.Key)).Value
                      ?? ResponseContent;

        return Task.FromResult(
            new HttpResponseMessage(ResponseStatusCode)
            {
                Content = new StringContent(content)
            }
        );
    }
}
