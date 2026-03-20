using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using MyHomeLibNG.Infrastructure.Providers.Online;

namespace MyHomeLibNG.Infrastructure.Providers;

public sealed class BookProviderFactory : IBookProviderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TransientHttpExecutor _httpExecutor;
    private readonly IInpxCatalogParser _inpxCatalogParser;
    private readonly IOfflineLibraryFileSystem _offlineFileSystem;
    private readonly IOfflineBookLocationResolver _locationResolver;
    private readonly OfflineContentStorageRegistry _contentStorageRegistry;

    public BookProviderFactory(
        IHttpClientFactory httpClientFactory,
        TransientHttpExecutor httpExecutor,
        IInpxCatalogParser inpxCatalogParser,
        IOfflineLibraryFileSystem offlineFileSystem,
        IOfflineBookLocationResolver locationResolver,
        OfflineContentStorageRegistry contentStorageRegistry)
    {
        _httpClientFactory = httpClientFactory;
        _httpExecutor = httpExecutor;
        _inpxCatalogParser = inpxCatalogParser;
        _offlineFileSystem = offlineFileSystem;
        _locationResolver = locationResolver;
        _contentStorageRegistry = contentStorageRegistry;
    }

    public IBookProvider CreateProvider(LibraryProfile profile)
    {
        return profile.ProviderId switch
        {
            BookProviderIds.OfflineInpx => new OfflineBookProvider(
                profile,
                _inpxCatalogParser,
                _offlineFileSystem,
                _locationResolver,
                _contentStorageRegistry),
            BookProviderIds.ProjectGutenberg => new ProjectGutenbergBookProvider(
                _httpClientFactory.CreateClient(HttpClientNames.ProjectGutenberg),
                _httpExecutor),
            BookProviderIds.OpenLibrary => new OpenLibraryBookProvider(
                _httpClientFactory.CreateClient(HttpClientNames.OpenLibrary),
                _httpExecutor),
            BookProviderIds.GoogleBooks => new GoogleBooksBookProvider(
                _httpClientFactory.CreateClient(HttpClientNames.GoogleBooks),
                _httpExecutor),
            _ => throw new InvalidOperationException($"Unknown provider '{profile.ProviderId}'.")
        };
    }
}
