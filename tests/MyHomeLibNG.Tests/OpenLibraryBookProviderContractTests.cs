using System.Net;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Providers;
using MyHomeLibNG.Infrastructure.Providers.Online;

namespace MyHomeLibNG.Tests;

public sealed class OpenLibraryBookProviderContractTests : BookProviderContractTestsBase
{
    protected override string SearchQuery => "pride";

    protected override IBookProvider CreateProvider()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, FixtureReader.ReadText(@"Fixtures\OpenLibrary\search.json"), "application/json");
        handler.Enqueue(HttpStatusCode.OK, FixtureReader.ReadText(@"Fixtures\OpenLibrary\details.json"), "application/json");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://openlibrary.test") };

        return new OpenLibraryBookProvider(client, new TransientHttpExecutor());
    }
}
