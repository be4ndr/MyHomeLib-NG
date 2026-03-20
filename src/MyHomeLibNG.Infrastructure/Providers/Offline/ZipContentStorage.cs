using System.IO.Compression;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class ZipContentStorage : IOfflineContentStorage
{
    private readonly IOfflineLibraryFileSystem _fileSystem;

    public ZipContentStorage(IOfflineLibraryFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool CanOpen(OfflineBookLocation location)
    {
        return !string.IsNullOrWhiteSpace(location.ArchiveEntryPath) &&
               string.Equals(Path.GetExtension(location.ContainerPath), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Stream> OpenReadAsync(OfflineBookLocation location, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var archiveStream = await _fileSystem.OpenReadAsync(location.ContainerPath, cancellationToken);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.GetEntry(location.ArchiveEntryPath!)
                        ?? throw new FileNotFoundException($"Archive entry '{location.ArchiveEntryPath}' was not found.");

            await using var entryStream = entry.Open();
            var content = new MemoryStream();
            await entryStream.CopyToAsync(content, cancellationToken);
            content.Position = 0;
            return content;
        }
        catch (InvalidDataException exception)
        {
            throw new IOException($"ZIP archive '{location.ContainerPath}' is invalid.", exception);
        }
    }
}
