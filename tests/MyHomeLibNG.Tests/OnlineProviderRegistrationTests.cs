using System.Net;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers;
using MyHomeLibNG.Infrastructure.Providers.Online;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class OnlineProviderRegistrationTests
{
    [Fact]
    public async Task OpenLibraryRegistration_UsesProfileApiBaseUrl()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(request =>
        {
            Assert.Equal("https://custom-openlibrary.test/search.json?q=dune", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"docs":[]}""")
            };
        });

        var profile = new LibraryProfile
        {
            Id = 1,
            Name = "Custom Open Library",
            ProviderId = BookProviderIds.OpenLibrary,
            LibraryType = LibraryType.Online,
            OnlineSource = new OnlineLibrarySourceSettings
            {
                ApiBaseUrl = "https://custom-openlibrary.test"
            }
        };
        var clients = new Dictionary<string, HttpClient>
        {
            ["providers.open-library"] = new(handler) { BaseAddress = new Uri("https://openlibrary.test") }
        };
        var registration = new OpenLibraryBookProviderRegistration(
            new FakeHttpClientFactory(clients),
            new TransientHttpExecutor());

        var provider = registration.Create(profile);

        await provider.SearchAsync("dune");
    }
}
