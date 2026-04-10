using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookProviderRegistration : IBookProviderRegistration
{
    private readonly ILibraryRepository _libraryRepository;
    private readonly IOfflineCatalogCache _catalogCache;
    private readonly IOfflineBookLocationResolver _locationResolver;
    private readonly OfflineContentStorageRegistry _contentStorageRegistry;

    public OfflineBookProviderRegistration(
        ILibraryRepository libraryRepository,
        IOfflineCatalogCache catalogCache,
        IOfflineBookLocationResolver locationResolver,
        OfflineContentStorageRegistry contentStorageRegistry)
    {
        _libraryRepository = libraryRepository;
        _catalogCache = catalogCache;
        _locationResolver = locationResolver;
        _contentStorageRegistry = contentStorageRegistry;
    }

    public string ProviderId => BookProviderIds.OfflineInpx;

    public bool CanCreate(LibraryProfile profile)
        => string.Equals(profile.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase);

    public IBookProvider Create(LibraryProfile profile)
        => new OfflineBookProvider(profile, _libraryRepository, _catalogCache, _locationResolver, _contentStorageRegistry);
}
