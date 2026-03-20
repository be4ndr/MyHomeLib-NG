namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineContentStorageRegistry
{
    private readonly IReadOnlyList<IOfflineContentStorage> _storages;

    public OfflineContentStorageRegistry(IEnumerable<IOfflineContentStorage> storages)
    {
        _storages = storages.ToArray();
    }

    public Task<Stream> OpenReadAsync(OfflineBookLocation location, CancellationToken cancellationToken = default)
    {
        var storage = _storages.FirstOrDefault(candidate => candidate.CanOpen(location));
        if (storage is null)
        {
            throw new NotSupportedException($"No content storage is registered for '{location.ContainerPath}'.");
        }

        return storage.OpenReadAsync(location, cancellationToken);
    }
}
