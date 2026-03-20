using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class OfflineBookLocationResolverTests
{
    [Fact]
    public async Task ResolveAsync_FallsBackToRecursiveScan()
    {
        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile("/library/nested/book.fb2", [1, 2, 3]);
        var resolver = new OfflineBookLocationResolver(fileSystem);

        var location = await resolver.ResolveAsync(
            new FolderLibrarySourceSettings
            {
                InpxFilePath = "/library/catalog.inpx",
                ArchiveDirectoryPath = "/library"
            },
            new OfflineCatalogEntry
            {
                Book = new() { Source = "Offline", SourceId = "book-1", Title = "Book" },
                ContainerPath = "missing/book.fb2"
            });

        Assert.NotNull(location);
        Assert.Equal("/library/nested/book.fb2", location.ContainerPath);
    }
}
