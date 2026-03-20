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
            new InpxCatalogParser(),
            fileSystem,
            new OfflineBookLocationResolver(fileSystem),
            new OfflineContentStorageRegistry(
            [
                new FileSystemContentStorage(fileSystem),
                new ZipContentStorage(fileSystem)
            ]));
    }
}
