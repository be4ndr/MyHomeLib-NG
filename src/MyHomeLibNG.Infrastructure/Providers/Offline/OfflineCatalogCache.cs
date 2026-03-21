using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineCatalogCache : IOfflineCatalogCache
{
    private readonly IInpxCatalogParser _catalogParser;
    private readonly IOfflineLibraryFileSystem _fileSystem;
    private readonly object _sync = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public OfflineCatalogCache(
        IInpxCatalogParser catalogParser,
        IOfflineLibraryFileSystem fileSystem)
    {
        _catalogParser = catalogParser;
        _fileSystem = fileSystem;
    }

    public Task<IReadOnlyList<OfflineCatalogEntry>> GetCatalogAsync(
        LibraryProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceSettings = profile.FolderSource
            ?? throw new InvalidOperationException("Offline provider requires folder source settings.");
        var metadata = _fileSystem.GetMetadata(sourceSettings.InpxFilePath)
            ?? throw new FileNotFoundException(
                $"Offline catalog '{sourceSettings.InpxFilePath}' was not found.",
                sourceSettings.InpxFilePath);

        var profileKey = BuildProfileKey(profile, sourceSettings.InpxFilePath);
        var cacheKey = new OfflineCatalogCacheKey(
            profileKey,
            sourceSettings.InpxFilePath,
            metadata.LengthBytes,
            metadata.LastWriteTimeUtc);

        CacheEntry cacheEntry;
        lock (_sync)
        {
            if (_entries.TryGetValue(profileKey, out var existingEntry) &&
                existingEntry.CacheKey == cacheKey)
            {
                return existingEntry.CatalogTask.WaitAsync(cancellationToken);
            }

            cacheEntry = new CacheEntry(cacheKey, LoadCatalogCoreAsync(profile, sourceSettings.InpxFilePath));
            _entries[profileKey] = cacheEntry;
        }

        return AwaitCatalogAsync(profileKey, cacheEntry, cancellationToken);
    }

    private async Task<IReadOnlyList<OfflineCatalogEntry>> LoadCatalogCoreAsync(
        LibraryProfile profile,
        string inpxFilePath)
    {
        await using var stream = await _fileSystem.OpenReadAsync(inpxFilePath);
        return await _catalogParser.ParseAsync(stream, profile.Name);
    }

    private async Task<IReadOnlyList<OfflineCatalogEntry>> AwaitCatalogAsync(
        string profileKey,
        CacheEntry cacheEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cacheEntry.CatalogTask.WaitAsync(cancellationToken);
        }
        catch
        {
            lock (_sync)
            {
                if (_entries.TryGetValue(profileKey, out var currentEntry) &&
                    ReferenceEquals(currentEntry, cacheEntry))
                {
                    _entries.Remove(profileKey);
                }
            }

            throw;
        }
    }

    private static string BuildProfileKey(LibraryProfile profile, string inpxFilePath)
    {
        return profile.Id > 0
            ? $"{profile.Id}|{inpxFilePath}"
            : $"{profile.ProviderId}|{profile.Name}|{inpxFilePath}";
    }

    private sealed record OfflineCatalogCacheKey(
        string ProfileKey,
        string InpxFilePath,
        long LengthBytes,
        DateTimeOffset LastWriteTimeUtc);

    private sealed class CacheEntry
    {
        public CacheEntry(OfflineCatalogCacheKey cacheKey, Task<IReadOnlyList<OfflineCatalogEntry>> catalogTask)
        {
            CacheKey = cacheKey;
            CatalogTask = catalogTask;
        }

        public OfflineCatalogCacheKey CacheKey { get; }
        public Task<IReadOnlyList<OfflineCatalogEntry>> CatalogTask { get; }
    }
}
