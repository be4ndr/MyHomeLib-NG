using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookProvider : IBookProvider
{
    private readonly LibraryProfile _profile;
    private readonly ILibraryRepository _libraryRepository;
    private readonly IOfflineCatalogCache _catalogCache;
    private readonly IOfflineBookLocationResolver _locationResolver;
    private readonly OfflineContentStorageRegistry _contentStorageRegistry;

    public OfflineBookProvider(
        LibraryProfile profile,
        ILibraryRepository libraryRepository,
        IOfflineCatalogCache catalogCache,
        IOfflineBookLocationResolver locationResolver,
        OfflineContentStorageRegistry contentStorageRegistry)
    {
        _profile = profile;
        _libraryRepository = libraryRepository;
        _catalogCache = catalogCache;
        _locationResolver = locationResolver;
        _contentStorageRegistry = contentStorageRegistry;
    }

    public OfflineBookProvider(
        LibraryProfile profile,
        ILibraryRepository libraryRepository,
        IInpxCatalogParser catalogParser,
        IOfflineLibraryFileSystem fileSystem,
        IOfflineBookLocationResolver locationResolver,
        OfflineContentStorageRegistry contentStorageRegistry)
        : this(
            profile,
            libraryRepository,
            new OfflineCatalogCache(catalogParser, fileSystem),
            locationResolver,
            contentStorageRegistry)
    {
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
        var entry = catalog
            .FirstOrDefault(candidate => string.Equals(candidate.Book.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return null;
        }

        return await EnrichBookAsync(entry, cancellationToken);
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
        return _catalogCache.GetCatalogAsync(_profile, cancellationToken);
    }

    private async Task<NormalizedBook> EnrichBookAsync(
        OfflineCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        var sourceSettings = _profile.FolderSource;
        if (sourceSettings is null)
        {
            return entry.Book;
        }

        var location = await _locationResolver.ResolveAsync(sourceSettings, entry, cancellationToken);
        if (location is null || string.IsNullOrWhiteSpace(location.ArchiveEntryPath))
        {
            return entry.Book;
        }

        var importedMetadata = await _libraryRepository.GetImportedBookMetadataAsync(
            _profile.Id,
            location.ContainerPath,
            location.ArchiveEntryPath,
            cancellationToken);

        if (importedMetadata is null)
        {
            return entry.Book;
        }

        return new NormalizedBook
        {
            Title = string.IsNullOrWhiteSpace(importedMetadata.Title) ? entry.Book.Title : importedMetadata.Title,
            Source = entry.Book.Source,
            SourceId = entry.Book.SourceId,
            Authors = MergeAuthors(entry.Book.Authors, importedMetadata.Authors),
            Series = importedMetadata.Series ?? entry.Book.Series,
            Language = importedMetadata.Language ?? entry.Book.Language,
            Description = importedMetadata.Annotation ?? entry.Book.Description,
            Subjects = MergeSubjects(entry.Book.Subjects, importedMetadata.Genres),
            Publisher = entry.Book.Publisher,
            PublishedYear = importedMetadata.PublishYear ?? entry.Book.PublishedYear,
            Isbn10 = entry.Book.Isbn10,
            Isbn13 = entry.Book.Isbn13,
            CoverUrl = entry.Book.CoverUrl,
            CoverThumbnail = importedMetadata.CoverThumbnail,
            Formats = entry.Book.Formats,
            DownloadLinks = entry.Book.DownloadLinks,
            ReadLink = entry.Book.ReadLink,
            BorrowLink = entry.Book.BorrowLink
        };
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
               (!string.IsNullOrWhiteSpace(book.Series) && book.Series.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               book.Subjects.Any(subject => subject.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(book.Description) && book.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> MergeAuthors(IReadOnlyList<string> currentAuthors, string? importedAuthors)
    {
        var imported = SplitList(importedAuthors);
        return imported.Count == 0 ? currentAuthors : imported;
    }

    private static IReadOnlyList<string> MergeSubjects(IReadOnlyList<string> currentSubjects, string? importedGenres)
    {
        var imported = SplitList(importedGenres);
        return imported.Count == 0 ? currentSubjects : imported;
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
    }
}
