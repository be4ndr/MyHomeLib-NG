using System.Text;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task DeleteLibraryAsync_RemovesSelectedLibraryAndClearsWorkspaceState()
    {
        var alphaLibrary = CreateOnlineProfile(1, "Alpha Library");
        var betaLibrary = CreateOnlineProfile(2, "Beta Library");
        var repository = new FakeLibraryRepository(alphaLibrary, betaLibrary);
        var activeLibraryContext = new ActiveLibraryContext(repository);
        var viewModel = new MainWindowViewModel(
            new LibraryProfilesService(repository),
            new LibraryBooksService(new FakeBookProviderFactory(new FakeBookProvider()), activeLibraryContext),
            activeLibraryContext,
            repository,
            new FakeLibrarySourceResolver());

        await viewModel.InitializeAsync();
        viewModel.SearchQuery = "Ada";
        await viewModel.SearchAsync();
        await viewModel.OpenDirectoryModeAsync();

        Assert.Equal(2, viewModel.Libraries.Count);
        Assert.NotNull(viewModel.SelectedLibrary);
        Assert.NotEmpty(viewModel.Results);
        Assert.NotEmpty(viewModel.DirectoryEntries);
        Assert.NotEmpty(viewModel.DirectoryBooks);
        Assert.True(activeLibraryContext.HasActiveLibrary);

        await viewModel.DeleteLibraryAsync(viewModel.SelectedLibrary!);

        Assert.Single(viewModel.Libraries);
        Assert.Equal("Beta Library", viewModel.Libraries[0].Name);
        Assert.Null(viewModel.SelectedLibrary);
        Assert.False(viewModel.HasSelectedLibrary);
        Assert.Equal("No library selected", viewModel.ActiveLibraryName);
        Assert.Empty(viewModel.Results);
        Assert.Empty(viewModel.DirectoryEntries);
        Assert.Empty(viewModel.DirectoryBooks);
        Assert.Null(viewModel.SelectedBook);
        Assert.Null(viewModel.SelectedBookDetails);
        Assert.False(activeLibraryContext.HasActiveLibrary);
    }

    [Theory]
    [InlineData("Ada Author", "author-match")]
    [InlineData("Chronicles", "series-match")]
    [InlineData("Fantasy", "genre-match")]
    [InlineData("secret annotation", "annotation-match")]
    public async Task SearchAsync_KeywordSearchMatchesExpandedMetadataFields(string query, string expectedSourceId)
    {
        var profile = CreateOnlineProfile(7, "Expanded Search");
        var repository = new FakeLibraryRepository(profile);
        var activeLibraryContext = new ActiveLibraryContext(repository);
        var viewModel = new MainWindowViewModel(
            new LibraryProfilesService(repository),
            new LibraryBooksService(new FakeBookProviderFactory(new FakeBookProvider()), activeLibraryContext),
            activeLibraryContext,
            repository,
            new FakeLibrarySourceResolver());

        await viewModel.InitializeAsync();
        viewModel.SearchQuery = query;

        await viewModel.SearchAsync();

        var result = Assert.Single(viewModel.Results);
        Assert.Equal(expectedSourceId, result.Book.Source.SourceId);
    }

    [Fact]
    public async Task SearchAsync_SeriesFilterUsesSeriesMetadata()
    {
        var profile = CreateOnlineProfile(8, "Series Search");
        var repository = new FakeLibraryRepository(profile);
        var activeLibraryContext = new ActiveLibraryContext(repository);
        var viewModel = new MainWindowViewModel(
            new LibraryProfilesService(repository),
            new LibraryBooksService(new FakeBookProviderFactory(new FakeBookProvider()), activeLibraryContext),
            activeLibraryContext,
            repository,
            new FakeLibrarySourceResolver());

        await viewModel.InitializeAsync();
        viewModel.SearchSeries = "Chronicles";

        await viewModel.SearchAsync();

        var result = Assert.Single(viewModel.Results);
        Assert.Equal("series-match", result.Book.Source.SourceId);
    }

    private static LibraryProfile CreateOnlineProfile(long id, string name)
    {
        return new LibraryProfile
        {
            Id = id,
            Name = name,
            ProviderId = BookProviderIds.ProjectGutenberg,
            LibraryType = LibraryType.Online,
            OnlineSource = new OnlineLibrarySourceSettings
            {
                ApiBaseUrl = "https://example.test",
                SearchEndpoint = "https://example.test/search"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeLibraryRepository : ILibraryRepository
    {
        private readonly Dictionary<long, LibraryProfile> _profiles;
        private long _nextId;

        public FakeLibraryRepository(params LibraryProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
            _nextId = profiles.Length == 0 ? 1 : profiles.Max(profile => profile.Id) + 1;
        }

        public Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<LibraryProfile>>(_profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        public Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_profiles.TryGetValue(libraryId, out var profile) ? profile : null);
        }

        public Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assignedId = profile.Id > 0 ? profile.Id : _nextId++;
            _profiles[assignedId] = new LibraryProfile
            {
                Id = assignedId,
                Name = profile.Name,
                ProviderId = profile.ProviderId,
                LibraryType = profile.LibraryType,
                OnlineSource = profile.OnlineSource,
                FolderSource = profile.FolderSource,
                CreatedAtUtc = profile.CreatedAtUtc,
                LastOpenedAtUtc = profile.LastOpenedAtUtc
            };
            return Task.FromResult(assignedId);
        }

        public Task<ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
            long libraryProfileId,
            string archivePath,
            string entryPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ImportedBookMetadataSnapshot?>(null);
        }

        public Task<long> GetImportedBookCountAsync(long libraryProfileId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0L);
        }

        public Task<IReadOnlyList<ImportedBookMetadataSnapshot>> SearchImportedBooksAsync(
            long libraryProfileId,
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ImportedBookMetadataSnapshot>>(Array.Empty<ImportedBookMetadataSnapshot>());
        }

        public Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public Task<BookImportBatchResult> UpsertImportedBooksAsync(
            IReadOnlyList<BookImportRecord> books,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException();
        }

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _profiles.Remove(libraryId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibrarySourceResolver : ILibrarySourceResolver
    {
        public Task<LibraryStructure> ResolveAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LibraryStructure
            {
                LibraryProfileId = profile.Id,
                LibraryType = LibraryType.Online,
                Sources =
                [
                    new LibrarySourceLocation
                    {
                        Kind = SourceKind.Http,
                        PathOrUri = profile.OnlineSource?.ApiBaseUrl ?? "https://example.test",
                        Description = "Online API base URL",
                        Exists = true
                    },
                    new LibrarySourceLocation
                    {
                        Kind = SourceKind.Http,
                        PathOrUri = profile.OnlineSource?.SearchEndpoint ?? "https://example.test/search",
                        Description = "Online search endpoint",
                        Exists = true
                    }
                ]
            });
        }
    }

    private sealed class FakeBookProviderFactory : IBookProviderFactory
    {
        private readonly IBookProvider _provider;

        public FakeBookProviderFactory(IBookProvider provider)
        {
            _provider = provider;
        }

        public IBookProvider CreateProvider(LibraryProfile profile)
            => _provider;
    }

    private sealed class FakeBookProvider : IBookProvider
    {
        private readonly Dictionary<string, NormalizedBook> _books = new(StringComparer.Ordinal)
        {
            ["author-match"] = new()
            {
                Title = "Plain Title",
                Source = "Project Gutenberg",
                SourceId = "author-match",
                Authors = ["Ada Author"],
                Description = "Author-only match",
                Formats = ["epub"],
                Subjects = ["Reference"],
                Language = "en",
                PublishedYear = 2024,
                ReadLink = "https://example.test/books/author-match"
            },
            ["series-match"] = new()
            {
                Title = "Volume 2",
                Source = "Project Gutenberg",
                SourceId = "series-match",
                Authors = ["Series Writer"],
                Series = "Chronicles",
                Description = "Series-only match",
                Formats = ["epub"],
                Subjects = ["Reference"],
                Language = "en",
                PublishedYear = 2025,
                ReadLink = "https://example.test/books/series-match"
            },
            ["genre-match"] = new()
            {
                Title = "Genre Atlas",
                Source = "Project Gutenberg",
                SourceId = "genre-match",
                Authors = ["Genre Writer"],
                Description = "Genre-only match",
                Formats = ["epub"],
                Subjects = ["Fantasy"],
                Language = "en",
                PublishedYear = 2023,
                ReadLink = "https://example.test/books/genre-match"
            },
            ["annotation-match"] = new()
            {
                Title = "Annotation Atlas",
                Source = "Project Gutenberg",
                SourceId = "annotation-match",
                Authors = ["Annotation Writer"],
                Description = "Contains a secret annotation token.",
                Formats = ["epub"],
                Subjects = ["Reference"],
                Language = "en",
                PublishedYear = 2022,
                ReadLink = "https://example.test/books/annotation-match"
            }
        };

        public string Id => BookProviderIds.ProjectGutenberg;
        public string DisplayName => "Fake provider";
        public BookProviderCapabilities Capabilities { get; } = new()
        {
            SupportsSearch = true,
            SupportsDetails = true,
            SupportsContentStream = false
        };

        public Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<NormalizedBook> results = _books.Values.ToArray();
            return Task.FromResult(results);
        }

        public Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_books.TryGetValue(sourceId, out var book) ? book : null);
        }

        public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(sourceId));
            return Task.FromResult(stream);
        }
    }
}
