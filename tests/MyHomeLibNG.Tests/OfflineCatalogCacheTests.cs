using MyHomeLibNG.Infrastructure.Providers.Offline;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class OfflineCatalogCacheTests
{
    [Fact]
    public async Task ReusesCatalogAcrossProviderInstances()
    {
        var profile = SampleOfflineFixture.CreateProfile();
        var fileSystem = SampleOfflineFixture.CreateFileSystem();
        var parser = new CountingInpxCatalogParser(new InpxCatalogParser());
        var cache = new OfflineCatalogCache(parser, fileSystem);
        var locationResolver = new OfflineBookLocationResolver(fileSystem);
        var storageRegistry = new OfflineContentStorageRegistry(
        [
            new FileSystemContentStorage(fileSystem),
            new ZipContentStorage(fileSystem)
        ]);

        var repository = new NullImportedMetadataRepository();
        var firstProvider = new OfflineBookProvider(profile, repository, cache, locationResolver, storageRegistry);
        var secondProvider = new OfflineBookProvider(profile, repository, cache, locationResolver, storageRegistry);

        await firstProvider.SearchAsync("Book");
        await secondProvider.GetByIdAsync("offline-file-1");
        await using var stream = await secondProvider.OpenContentAsync("offline-zip-1");

        Assert.True(stream.CanRead);
        Assert.Equal(1, parser.CallCount);
    }

    [Fact]
    public async Task InvalidatesCatalogWhenInpxMetadataChanges()
    {
        var profile = SampleOfflineFixture.CreateProfile();
        var fileSystem = SampleOfflineFixture.CreateFileSystem();
        var parser = new CountingInpxCatalogParser(new InpxCatalogParser());
        var cache = new OfflineCatalogCache(parser, fileSystem);

        await cache.GetCatalogAsync(profile);

        var metadata = fileSystem.GetMetadata(SampleOfflineFixture.InpxPath);
        Assert.NotNull(metadata);
        fileSystem.UpdateLastWriteTimeUtc(
            SampleOfflineFixture.InpxPath,
            metadata!.LastWriteTimeUtc.AddMinutes(5));

        await cache.GetCatalogAsync(profile);

        Assert.Equal(2, parser.CallCount);
    }

    private sealed class CountingInpxCatalogParser : IInpxCatalogParser
    {
        private readonly IInpxCatalogParser _inner;

        public CountingInpxCatalogParser(IInpxCatalogParser inner)
        {
            _inner = inner;
        }

        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<OfflineCatalogEntry>> ParseAsync(
            Stream stream,
            string sourceName,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await _inner.ParseAsync(stream, sourceName, cancellationToken);
        }
    }

    private sealed class NullImportedMetadataRepository : MyHomeLibNG.Core.Interfaces.ILibraryRepository
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

        public Task<long> UpsertImportedBookAsync(MyHomeLibNG.Core.Models.BookImportRecord book, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
