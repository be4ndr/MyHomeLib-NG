using System.Text;
using MyHomeLibNG.App.ViewModels;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.HeadlessTests.TestDoubles;

internal static class TestViewModelFactory
{
    public static MainWindowViewModel CreateEmptyWorkspace()
    {
        return CreateViewModel();
    }

    public static MainWindowViewModel CreateWithOnlineLibrary(string name = "Test Library")
    {
        var profile = CreateOnlineProfile(1, name);
        return CreateViewModel(profile);
    }

    private static MainWindowViewModel CreateViewModel(params LibraryProfile[] profiles)
    {
        var repository = new FakeLibraryRepository(profiles);
        var activeLibraryContext = new ActiveLibraryContext(repository);

        return new MainWindowViewModel(
            new LibraryProfilesService(repository),
            new LibraryBooksService(new FakeBookProviderFactory(new FakeBookProvider()), activeLibraryContext),
            activeLibraryContext,
            repository,
            new FakeLibrarySourceResolver());
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
        private readonly IReadOnlyList<NormalizedBook> _books =
        [
            new()
            {
                Title = "Dune",
                Source = "Project Gutenberg",
                SourceId = "dune",
                Authors = ["Frank Herbert"],
                Series = "Dune",
                Description = "Science fiction classic.",
                Formats = ["epub"],
                Subjects = ["Science Fiction"],
                Language = "en",
                PublishedYear = 1965,
                ReadLink = "https://example.test/books/dune"
            },
            new()
            {
                Title = "Foundation",
                Source = "Project Gutenberg",
                SourceId = "foundation",
                Authors = ["Isaac Asimov"],
                Series = "Foundation",
                Description = "A galactic empire story.",
                Formats = ["epub"],
                Subjects = ["Science Fiction"],
                Language = "en",
                PublishedYear = 1951,
                ReadLink = "https://example.test/books/foundation"
            }
        ];

        public string Id => BookProviderIds.ProjectGutenberg;
        public string DisplayName => "Fake provider";
        public BookProviderCapabilities Capabilities { get; } = new()
        {
            SupportsSearch = true,
            SupportsDetails = true,
            SupportsContentStream = true
        };

        public Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_books);
        }

        public Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_books.FirstOrDefault(book => string.Equals(book.SourceId, sourceId, StringComparison.Ordinal)));
        }

        public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(sourceId)));
        }
    }
}
