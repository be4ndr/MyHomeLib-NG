using System.Net;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Providers;
using MyHomeLibNG.Infrastructure.Providers.Online;

namespace MyHomeLibNG.Tests;

public sealed class ProjectGutenbergBookProviderContractTests : BookProviderContractTestsBase
{
    protected override string SearchQuery => "pride";

    protected override IBookProvider CreateProvider()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.OK, FixtureReader.ReadText(@"Fixtures\ProjectGutenberg\search.opds"), "application/xml");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gutenberg.test") };

        return new ProjectGutenbergBookProvider(client, new TransientHttpExecutor());
    }
}
