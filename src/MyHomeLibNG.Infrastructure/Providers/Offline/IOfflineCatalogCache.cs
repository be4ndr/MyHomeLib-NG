using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public interface IOfflineCatalogCache
{
    Task<IReadOnlyList<OfflineCatalogEntry>> GetCatalogAsync(
        LibraryProfile profile,
        CancellationToken cancellationToken = default);
}
