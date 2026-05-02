using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class OfflineBookProviderContractTests : BookProviderContractTestsBase
{
    protected override string SearchQuery => "Book";

    protected override IBookProvider CreateProvider()
    {
        var fileSystem = SampleOfflineFixture.CreateFileSystem();
        return new OfflineBookProvider(
            SampleOfflineFixture.CreateProfile(),
            new NullImportedMetadataRepository(),
            new InpxCatalogParser(),
            fileSystem,
            new OfflineBookLocationResolver(fileSystem),
            new OfflineContentStorageRegistry(
            [
                new FileSystemContentStorage(fileSystem),
                new ZipContentStorage(fileSystem)
            ]));
    }

    [Fact]
    public async Task SearchAsync_UsesImportedIndexWhenAvailable()
    {
        var fileSystem = SampleOfflineFixture.CreateFileSystem();
        var repository = new IndexedRepository(
        [
            new ImportedBookMetadataSnapshot
            {
                Title = "Indexed Archive Book",
                Authors = "Indexed Author",
                Annotation = "annotation token",
                Genres = "fantasy",
                Language = "ru",
                ArchivePath = SampleOfflineFixture.ZipPath,
                EntryPath = "books/archive-book.fb2",
                FileName = "archive-book.fb2",
                FileSize = 321
            }
        ]);
        var provider = new OfflineBookProvider(
            SampleOfflineFixture.CreateProfile(),
            repository,
            new InpxCatalogParser(),
            fileSystem,
            new OfflineBookLocationResolver(fileSystem),
            new OfflineContentStorageRegistry(
            [
                new FileSystemContentStorage(fileSystem),
                new ZipContentStorage(fileSystem)
            ]));

        var results = await provider.SearchAsync("annotation token");

        var result = Assert.Single(results);
        Assert.Equal("Indexed Archive Book", result.Title);
        Assert.Equal(["Indexed Author"], result.Authors);
        Assert.Equal(["fantasy"], result.Subjects);
    }

    [Fact]
    public async Task OpenContentAsync_OpensImportedArchiveEntryWithoutCatalogLookup()
    {
        var fileSystem = SampleOfflineFixture.CreateFileSystem();
        var repository = new IndexedRepository(
        [
            new ImportedBookMetadataSnapshot
            {
                Title = "Indexed Archive Book",
                ArchivePath = SampleOfflineFixture.ZipPath,
                EntryPath = "books/archive-book.fb2",
                FileName = "archive-book.fb2",
                FileSize = 321
            }
        ]);
        var provider = new OfflineBookProvider(
            SampleOfflineFixture.CreateProfile(),
            repository,
            new InpxCatalogParser(),
            fileSystem,
            new OfflineBookLocationResolver(fileSystem),
            new OfflineContentStorageRegistry(
            [
                new FileSystemContentStorage(fileSystem),
                new ZipContentStorage(fileSystem)
            ]));

        var result = Assert.Single(await provider.SearchAsync("Indexed Archive Book"));
        await using var stream = await provider.OpenContentAsync(result.SourceId);
        using var reader = new StreamReader(stream);

        Assert.Equal("zip archive content", await reader.ReadToEndAsync());
    }

    private sealed class NullImportedMetadataRepository : ILibraryRepository
    {
        public Task<IReadOnlyList<MyHomeLibNG.Core.Models.LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MyHomeLibNG.Core.Models.LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> AddAsync(MyHomeLibNG.Core.Models.LibraryProfile profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MyHomeLibNG.Core.Models.ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
            long libraryProfileId,
            string archivePath,
            string entryPath,
            CancellationToken cancellationToken = default)
            => Task.FromResult<MyHomeLibNG.Core.Models.ImportedBookMetadataSnapshot?>(null);

        public Task<long> GetImportedBookCountAsync(long libraryProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<IReadOnlyList<MyHomeLibNG.Core.Models.ImportedBookMetadataSnapshot>> SearchImportedBooksAsync(
            long libraryProfileId,
            string query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MyHomeLibNG.Core.Models.ImportedBookMetadataSnapshot>>(Array.Empty<MyHomeLibNG.Core.Models.ImportedBookMetadataSnapshot>());

        public Task<long> UpsertImportedBookAsync(MyHomeLibNG.Core.Models.BookImportRecord book, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MyHomeLibNG.Core.Models.BookImportBatchResult> UpsertImportedBooksAsync(
            IReadOnlyList<MyHomeLibNG.Core.Models.BookImportRecord> books,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class IndexedRepository : ILibraryRepository
    {
        private readonly IReadOnlyList<ImportedBookMetadataSnapshot> _books;

        public IndexedRepository(IReadOnlyList<ImportedBookMetadataSnapshot> books)
        {
            _books = books;
        }

        public Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
            long libraryProfileId,
            string archivePath,
            string entryPath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_books.FirstOrDefault(book =>
                string.Equals(book.ArchivePath, archivePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(book.EntryPath, entryPath, StringComparison.OrdinalIgnoreCase)));

        public Task<long> GetImportedBookCountAsync(long libraryProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult((long)_books.Count);

        public Task<IReadOnlyList<ImportedBookMetadataSnapshot>> SearchImportedBooksAsync(
            long libraryProfileId,
            string query,
            CancellationToken cancellationToken = default)
        {
            var matches = string.IsNullOrWhiteSpace(query)
                ? _books
                : _books.Where(book =>
                        (book.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (book.Authors?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (book.Series?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (book.Genres?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (book.Annotation?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToArray();

            return Task.FromResult<IReadOnlyList<ImportedBookMetadataSnapshot>>(matches);
        }

        public Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BookImportBatchResult> UpsertImportedBooksAsync(
            IReadOnlyList<BookImportRecord> books,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
