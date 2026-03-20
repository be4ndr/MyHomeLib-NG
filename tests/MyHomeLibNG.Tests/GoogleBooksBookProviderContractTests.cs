using System.Net;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Providers;
using MyHomeLibNG.Infrastructure.Providers.Online;

namespace MyHomeLibNG.Tests;

public sealed class GoogleBooksBookProviderContractTests : BookProviderContractTestsBase
{
    protected override string SearchQuery => "time machine";

    protected override IBookProvider CreateProvider()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, FixtureReader.ReadText(@"Fixtures\GoogleBooks\search.json"), "application/json");
        handler.Enqueue(HttpStatusCode.OK, FixtureReader.ReadText(@"Fixtures\GoogleBooks\details.json"), "application/json");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://googlebooks.test") };

        return new GoogleBooksBookProvider(client, new TransientHttpExecutor());
    }
}
