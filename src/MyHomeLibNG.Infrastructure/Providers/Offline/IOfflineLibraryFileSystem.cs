namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public interface IOfflineLibraryFileSystem
{
    bool FileExists(string path);
    IReadOnlyList<string> EnumerateFilesRecursive(string rootPath);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);
}
