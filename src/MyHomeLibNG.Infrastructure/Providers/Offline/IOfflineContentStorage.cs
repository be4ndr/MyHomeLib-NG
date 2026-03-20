namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public interface IOfflineContentStorage
{
    bool CanOpen(OfflineBookLocation location);
    Task<Stream> OpenReadAsync(OfflineBookLocation location, CancellationToken cancellationToken = default);
}
