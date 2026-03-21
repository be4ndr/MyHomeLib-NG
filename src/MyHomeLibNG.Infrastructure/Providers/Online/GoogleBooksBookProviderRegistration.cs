using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class GoogleBooksBookProviderRegistration : IBookProviderRegistration
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TransientHttpExecutor _httpExecutor;

    public GoogleBooksBookProviderRegistration(
        IHttpClientFactory httpClientFactory,
        TransientHttpExecutor httpExecutor)
    {
        _httpClientFactory = httpClientFactory;
        _httpExecutor = httpExecutor;
    }

    public string ProviderId => BookProviderIds.GoogleBooks;

    public bool CanCreate(LibraryProfile profile)
        => string.Equals(profile.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase);

    public IBookProvider Create(LibraryProfile profile)
        => new GoogleBooksBookProvider(
            _httpClientFactory.CreateClient(HttpClientNames.GoogleBooks),
            _httpExecutor);
}
