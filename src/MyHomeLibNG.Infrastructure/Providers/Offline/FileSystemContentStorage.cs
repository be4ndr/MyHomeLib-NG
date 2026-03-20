namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class FileSystemContentStorage : IOfflineContentStorage
{
    private readonly IOfflineLibraryFileSystem _fileSystem;

    public FileSystemContentStorage(IOfflineLibraryFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool CanOpen(OfflineBookLocation location)
        => string.IsNullOrWhiteSpace(location.ArchiveEntryPath);

    public Task<Stream> OpenReadAsync(OfflineBookLocation location, CancellationToken cancellationToken = default)
        => _fileSystem.OpenReadAsync(location.ContainerPath, cancellationToken);
}
