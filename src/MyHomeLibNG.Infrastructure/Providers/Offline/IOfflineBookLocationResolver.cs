using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public interface IOfflineBookLocationResolver
{
    Task<OfflineBookLocation?> ResolveAsync(
        FolderLibrarySourceSettings sourceSettings,
        OfflineCatalogEntry entry,
        CancellationToken cancellationToken = default);
}
