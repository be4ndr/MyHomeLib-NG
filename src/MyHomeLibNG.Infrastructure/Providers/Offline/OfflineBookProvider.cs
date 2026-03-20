using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookProvider : IBookProvider
{
    private readonly LibraryProfile _profile;
    private readonly IInpxCatalogParser _catalogParser;
    private readonly IOfflineLibraryFileSystem _fileSystem;
    private readonly IOfflineBookLocationResolver _locationResolver;
    private readonly OfflineContentStorageRegistry _contentStorageRegistry;
    private Task<IReadOnlyList<OfflineCatalogEntry>>? _catalogTask;

    public OfflineBookProvider(
        LibraryProfile profile,
        IInpxCatalogParser catalogParser,
        IOfflineLibraryFileSystem fileSystem,
        IOfflineBookLocationResolver locationResolver,
        OfflineContentStorageRegistry contentStorageRegistry)
    {
        _profile = profile;
        _catalogParser = catalogParser;
        _fileSystem = fileSystem;
        _locationResolver = locationResolver;
        _contentStorageRegistry = contentStorageRegistry;
    }

    public string Id => _profile.ProviderId;
    public string DisplayName => _profile.Name;
    public BookProviderCapabilities Capabilities { get; } = new()
    {
        SupportsSearch = true,
        SupportsDetails = true,
        SupportsContentStream = true
    };

    public async Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);
        var matches = catalog
            .Where(entry => Matches(entry.Book, query))
            .Select(entry => entry.Book);

        return NormalizedBookDeduplicator.Deduplicate(matches);
    }

    public async Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);
        return catalog
            .FirstOrDefault(entry => string.Equals(entry.Book.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
            ?.Book;
    }

    public async Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var catalog = await LoadCatalogAsync(cancellationToken);
        var entry = catalog.FirstOrDefault(candidate =>
            string.Equals(candidate.Book.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            throw new FileNotFoundException($"Book '{sourceId}' was not found in the offline catalog.");
        }

        var sourceSettings = _profile.FolderSource
            ?? throw new InvalidOperationException("Offline provider requires folder source settings.");

        var location = await _locationResolver.ResolveAsync(sourceSettings, entry, cancellationToken);
        if (location is null)
        {
            throw new FileNotFoundException($"Book '{sourceId}' could not be resolved to a physical location.");
        }

        return await _contentStorageRegistry.OpenReadAsync(location, cancellationToken);
    }

    private Task<IReadOnlyList<OfflineCatalogEntry>> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        _catalogTask ??= LoadCatalogCoreAsync(cancellationToken);
        return _catalogTask;
    }

    private async Task<IReadOnlyList<OfflineCatalogEntry>> LoadCatalogCoreAsync(CancellationToken cancellationToken)
    {
        var sourceSettings = _profile.FolderSource
            ?? throw new InvalidOperationException("Offline provider requires folder source settings.");

        await using var stream = await _fileSystem.OpenReadAsync(sourceSettings.InpxFilePath, cancellationToken);
        return await _catalogParser.ParseAsync(stream, _profile.Name, cancellationToken);
    }

    private static bool Matches(NormalizedBook book, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return book.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               book.SourceId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               book.Authors.Any(author => author.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               book.Subjects.Any(subject => subject.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
