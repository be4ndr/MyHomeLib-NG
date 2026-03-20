namespace MyHomeLibNG.Tests;

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients;

    public FakeHttpClientFactory(Dictionary<string, HttpClient> clients)
    {
        _clients = clients;
    }

    public HttpClient CreateClient(string name)
    {
        return _clients[name];
    }
}
