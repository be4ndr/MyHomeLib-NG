using System.Text;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class LibraryBooksServiceTests
{
    [Fact]
    public async Task SearchActiveLibraryAsync_UsesActiveLibraryAndMapsSearchResult()
    {
        var profile = CreateProfile(17, "Offline shelf");
        var repository = new FakeLibraryRepository(profile);
        var activeLibraryContext = new ActiveLibraryContext(repository);
        await activeLibraryContext.SetActiveAsync(profile.Id);

        var provider = new FakeBookProvider
        {
            SearchResults =
            [
                new NormalizedBook
                {
                    Title = "Book One",
                    Source = "Offline shelf",
                    SourceId = "offline-book-1",
                    Authors = ["Ana Author"],
                    Formats = ["fb2"]
                }
            ]
        };

        var service = new LibraryBooksService(
            new FakeBookProviderFactory(provider),
            activeLibraryContext);

        var results = await service.SearchActiveLibraryAsync("Book");

        var book = Assert.Single(results);
        Assert.Equal(profile.Id, book.Source.LibraryProfileId);
        Assert.Equal(profile.ProviderId, book.Source.ProviderId);
        Assert.Equal("offline-book-1", book.Source.SourceId);
        Assert.NotNull(book.ContentHandle);
        Assert.Equal("offline-book-1", book.ContentHandle!.SourceId);
    }

    [Fact]
    public async Task OpenBookContentFromActiveLibraryAsync_UsesActiveLibraryProvider()
    {
        var profile = CreateProfile(18, "Offline shelf");
        var repository = new FakeLibraryRepository(profile);
        var activeLibraryContext = new ActiveLibraryContext(repository);
        await activeLibraryContext.SetActiveAsync(profile.Id);

        var provider = new FakeBookProvider();
        var service = new LibraryBooksService(
            new FakeBookProviderFactory(provider),
            activeLibraryContext);

        await using var stream = await service.OpenBookContentFromActiveLibraryAsync("offline-book-2");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        Assert.Equal("offline-book-2", provider.OpenedSourceId);
        Assert.Equal("content:offline-book-2", content);
    }

    private static LibraryProfile CreateProfile(long id, string name)
    {
        return new LibraryProfile
        {
            Id = id,
            Name = name,
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = $"/library/{id}.inpx",
                ArchiveDirectoryPath = "/library"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeLibraryRepository : ILibraryRepository
    {
        private readonly Dictionary<long, LibraryProfile> _profiles;

        public FakeLibraryRepository(params LibraryProfile[] profiles)
        {
            _profiles = profiles.ToDictionary(profile => profile.Id);
        }

        public Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LibraryProfile>>(_profiles.Values.ToArray());

        public Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
            => Task.FromResult(_profiles.TryGetValue(libraryId, out var profile) ? profile : null);

        public Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
        {
            _profiles[profile.Id] = profile;
            return Task.FromResult(profile.Id);
        }

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
        {
            _profiles.Remove(libraryId);
            return Task.CompletedTask;
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
        public string Id => BookProviderIds.OfflineInpx;
        public string DisplayName => "Fake provider";
        public BookProviderCapabilities Capabilities { get; } = new()
        {
            SupportsSearch = true,
            SupportsDetails = true,
            SupportsContentStream = true
        };

        public IReadOnlyList<NormalizedBook> SearchResults { get; init; } = Array.Empty<NormalizedBook>();
        public string? OpenedSourceId { get; private set; }

        public Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults);

        public Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults.FirstOrDefault(book => book.SourceId == sourceId));

        public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            OpenedSourceId = sourceId;
            Stream stream = new MemoryStream(Encoding.UTF8.GetBytes($"content:{sourceId}"));
            return Task.FromResult(stream);
        }
    }
}
