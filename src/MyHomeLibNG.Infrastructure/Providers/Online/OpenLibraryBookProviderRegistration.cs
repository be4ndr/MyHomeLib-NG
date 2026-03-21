using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class OpenLibraryBookProviderRegistration : IBookProviderRegistration
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TransientHttpExecutor _httpExecutor;

    public OpenLibraryBookProviderRegistration(
        IHttpClientFactory httpClientFactory,
        TransientHttpExecutor httpExecutor)
    {
        _httpClientFactory = httpClientFactory;
        _httpExecutor = httpExecutor;
    }

    public string ProviderId => BookProviderIds.OpenLibrary;

    public bool CanCreate(LibraryProfile profile)
        => string.Equals(profile.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase);

    public IBookProvider Create(LibraryProfile profile)
        => new OpenLibraryBookProvider(
            _httpClientFactory.CreateClient(HttpClientNames.OpenLibrary),
            _httpExecutor);
}
