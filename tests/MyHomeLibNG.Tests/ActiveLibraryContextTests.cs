using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class ActiveLibraryContextTests
{
    [Fact]
    public async Task SetActiveAsync_ById_LoadsAndCachesProfile()
    {
        var profile = CreateProfile(42, "Offline sample");
        var repository = new FakeLibraryRepository(profile);
        var context = new ActiveLibraryContext(repository);

        await context.SetActiveAsync(profile.Id);

        Assert.True(context.HasActiveLibrary);
        Assert.NotNull(context.Current);
        Assert.Equal(profile.Id, context.Current!.Id);
        Assert.Equal(profile.Name, context.Current.Name);
    }

    [Fact]
    public async Task Current_RemainsAvailable_AfterRepositoryEntryIsRemoved()
    {
        var profile = CreateProfile(100, "Cached library");
        var repository = new FakeLibraryRepository(profile);
        var context = new ActiveLibraryContext(repository);

        await context.SetActiveAsync(profile.Id);
        repository.Remove(profile.Id);

        Assert.True(context.HasActiveLibrary);
        Assert.NotNull(context.Current);
        Assert.Equal(profile.Id, context.Current!.Id);
    }

    [Fact]
    public async Task SetActiveAsync_ById_Throws_WhenProfileDoesNotExist()
    {
        var context = new ActiveLibraryContext(new FakeLibraryRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SetActiveAsync(999));
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

        public void Remove(long libraryId)
        {
            _profiles.Remove(libraryId);
        }
    }
}
