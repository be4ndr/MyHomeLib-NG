using MyHomeLibNG.Infrastructure.Providers.Offline;

namespace MyHomeLibNG.Tests;

internal sealed class InMemoryOfflineLibraryFileSystem : IOfflineLibraryFileSystem
{
    private readonly Dictionary<string, InMemoryFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, byte[] content, DateTimeOffset? lastWriteTimeUtc = null)
    {
        _files[path] = new InMemoryFile(content, lastWriteTimeUtc ?? DateTimeOffset.UtcNow);
    }

    public void UpdateLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc)
    {
        if (!_files.TryGetValue(path, out var file))
        {
            throw new FileNotFoundException(path);
        }

        _files[path] = file with { LastWriteTimeUtc = lastWriteTimeUtc };
    }

    public bool FileExists(string path)
        => _files.ContainsKey(path);

    public OfflineFileMetadata? GetMetadata(string path)
    {
        if (!_files.TryGetValue(path, out var file))
        {
            return null;
        }

        return new OfflineFileMetadata
        {
            LengthBytes = file.Content.LongLength,
            LastWriteTimeUtc = file.LastWriteTimeUtc
        };
    }

    public IReadOnlyList<string> EnumerateFilesRecursive(string rootPath)
    {
        return _files.Keys
            .Where(path => path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_files.TryGetValue(path, out var file))
        {
            throw new FileNotFoundException(path);
        }

        Stream stream = new MemoryStream(file.Content, writable: false);
        return Task.FromResult(stream);
    }

    private sealed record InMemoryFile(byte[] Content, DateTimeOffset LastWriteTimeUtc);
}
