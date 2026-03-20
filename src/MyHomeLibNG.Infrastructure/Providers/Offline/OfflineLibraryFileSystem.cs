namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineLibraryFileSystem : IOfflineLibraryFileSystem
{
    public bool FileExists(string path)
        => File.Exists(path);

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
