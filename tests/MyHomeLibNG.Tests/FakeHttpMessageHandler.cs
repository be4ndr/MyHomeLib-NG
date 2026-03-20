using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MyHomeLibNG.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public int CallCount { get; private set; }

    public void Enqueue(HttpStatusCode statusCode, string content, string mediaType)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        });
    }

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responses.Enqueue(responseFactory);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(_responses.Dequeue().Invoke(request));
    }
}
