using System.Text;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookProvider : IBookProvider
{
    private const string ImportedSourcePrefix = "imported:";

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
        if (_profile.Id > 0)
        {
            var importedBooks = await _libraryRepository.SearchImportedBooksAsync(_profile.Id, query, cancellationToken);
            if (importedBooks.Count > 0)
            {
                return importedBooks
                    .Select(MapImportedBook)
                    .ToArray();
            }
        }

        var catalog = await LoadCatalogAsync(cancellationToken);
        var matches = catalog
            .Where(entry => Matches(entry.Book, query))
            .Select(entry => entry.Book);

        return NormalizedBookDeduplicator.Deduplicate(matches);
    }

    public async Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (TryParseImportedSourceId(sourceId, out var archivePath, out var entryPath))
        {
            var importedBook = await _libraryRepository.GetImportedBookMetadataAsync(
                _profile.Id,
                archivePath,
                entryPath,
                cancellationToken);

            return importedBook is null ? null : MapImportedBook(importedBook);
        }

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
        if (TryParseImportedSourceId(sourceId, out var archivePath, out var entryPath))
        {
            return await _contentStorageRegistry.OpenReadAsync(new OfflineBookLocation
            {
                ContainerPath = archivePath,
                ArchiveEntryPath = entryPath
            }, cancellationToken);
        }

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

        return MergeCatalogAndImportedBook(entry.Book, importedMetadata);
    }

    private static NormalizedBook MergeCatalogAndImportedBook(NormalizedBook catalogBook, ImportedBookMetadataSnapshot importedMetadata)
    {
        return new NormalizedBook
        {
            Title = string.IsNullOrWhiteSpace(importedMetadata.Title) ? catalogBook.Title : importedMetadata.Title,
            Source = catalogBook.Source,
            SourceId = catalogBook.SourceId,
            Authors = MergeAuthors(catalogBook.Authors, importedMetadata.Authors),
            Series = importedMetadata.Series ?? catalogBook.Series,
            Language = importedMetadata.Language ?? catalogBook.Language,
            Description = importedMetadata.Annotation ?? catalogBook.Description,
            Subjects = MergeSubjects(catalogBook.Subjects, importedMetadata.Genres),
            Publisher = catalogBook.Publisher,
            PublishedYear = importedMetadata.PublishYear ?? catalogBook.PublishedYear,
            Isbn10 = catalogBook.Isbn10,
            Isbn13 = catalogBook.Isbn13,
            CoverUrl = catalogBook.CoverUrl,
            CoverThumbnail = importedMetadata.CoverThumbnail,
            Formats = catalogBook.Formats,
            DownloadLinks = catalogBook.DownloadLinks,
            ReadLink = catalogBook.ReadLink,
            BorrowLink = catalogBook.BorrowLink
        };
    }

    private NormalizedBook MapImportedBook(ImportedBookMetadataSnapshot book)
    {
        return new NormalizedBook
        {
            Title = book.Title,
            Source = _profile.Name,
            SourceId = CreateImportedSourceId(book.ArchivePath, book.EntryPath),
            Authors = SplitList(book.Authors),
            Series = book.Series,
            Language = book.Language,
            Description = book.Annotation,
            Subjects = SplitList(book.Genres),
            PublishedYear = book.PublishYear,
            CoverThumbnail = book.CoverThumbnail,
            Formats = BuildFormats(book.EntryPath, book.FileName)
        };
    }

    private static IReadOnlyList<string> BuildFormats(string entryPath, string? fileName)
    {
        var source = string.IsNullOrWhiteSpace(entryPath) ? fileName : entryPath;
        var extension = Path.GetExtension(source);
        return string.IsNullOrWhiteSpace(extension)
            ? Array.Empty<string>()
            : [extension.TrimStart('.').ToLowerInvariant()];
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

    private static string CreateImportedSourceId(string archivePath, string entryPath)
    {
        return ImportedSourcePrefix +
               Convert.ToBase64String(Encoding.UTF8.GetBytes(archivePath)) +
               ":" +
               Convert.ToBase64String(Encoding.UTF8.GetBytes(entryPath));
    }

    private static bool TryParseImportedSourceId(string sourceId, out string archivePath, out string entryPath)
    {
        archivePath = string.Empty;
        entryPath = string.Empty;

        if (!sourceId.StartsWith(ImportedSourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var encoded = sourceId[ImportedSourcePrefix.Length..];
        var separatorIndex = encoded.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= encoded.Length - 1)
        {
            return false;
        }

        try
        {
            archivePath = Encoding.UTF8.GetString(Convert.FromBase64String(encoded[..separatorIndex]));
            entryPath = Encoding.UTF8.GetString(Convert.FromBase64String(encoded[(separatorIndex + 1)..]));
            return !string.IsNullOrWhiteSpace(archivePath) && !string.IsNullOrWhiteSpace(entryPath);
        }
        catch (FormatException)
        {
            archivePath = string.Empty;
            entryPath = string.Empty;
            return false;
        }
    }
}
