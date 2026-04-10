using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Providers.Offline;

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

        public Task<long> UpsertImportedBookAsync(MyHomeLibNG.Core.Models.BookImportRecord book, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
