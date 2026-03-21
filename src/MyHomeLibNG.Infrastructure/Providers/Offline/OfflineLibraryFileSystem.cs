namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineLibraryFileSystem : IOfflineLibraryFileSystem
{
    public bool FileExists(string path)
        => File.Exists(path);

    public OfflineFileMetadata? GetMetadata(string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return null;
        }

        return new OfflineFileMetadata
        {
            LengthBytes = fileInfo.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
        };
    }

    public IReadOnlyList<string> EnumerateFilesRecursive(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories).ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}
