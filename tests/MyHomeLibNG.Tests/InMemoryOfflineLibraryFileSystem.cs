using MyHomeLibNG.Infrastructure.Providers.Offline;

namespace MyHomeLibNG.Tests;

internal sealed class InMemoryOfflineLibraryFileSystem : IOfflineLibraryFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, byte[] content)
    {
        _files[path] = content;
    }

    public bool FileExists(string path)
        => _files.ContainsKey(path);

    public IReadOnlyList<string> EnumerateFilesRecursive(string rootPath)
    {
        return _files.Keys
            .Where(path => path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_files.TryGetValue(path, out var content))
        {
            throw new FileNotFoundException(path);
        }

        Stream stream = new MemoryStream(content, writable: false);
        return Task.FromResult(stream);
    }
}
