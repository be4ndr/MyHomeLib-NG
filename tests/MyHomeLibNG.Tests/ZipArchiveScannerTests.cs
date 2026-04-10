using System.Text;
using MyHomeLibNG.Infrastructure.Import;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class ZipArchiveScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsOnlyFb2EntriesWithoutExtractingArchives()
    {
        const string rootPath = "/library";
        const string archivePath = "/library/archives/books.zip";

        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("nested/one.fb2", Fb2ImportTestData.CreateFb2(title: "One")),
            ("nested/two.txt", "ignore me"),
            ("two.fb2", Fb2ImportTestData.CreateFb2(title: "Two"))));
        fileSystem.AddFile("/library/archives/ignore.txt", Encoding.UTF8.GetBytes("plain text"));

        var scanner = new ZipArchiveScanner(fileSystem);
        var entries = new List<MyHomeLibNG.Core.Models.ZipArchiveBookEntry>();

        await foreach (var entry in scanner.ScanAsync(rootPath))
        {
            entries.Add(entry);
        }

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.EndsWith(".fb2", entry.EntryPath, StringComparison.OrdinalIgnoreCase));
        Assert.All(entries, entry => Assert.Equal(archivePath, entry.ArchivePath));

        await using var stream = await entries[0].OpenReadAsync(CancellationToken.None);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        Assert.Contains("FictionBook", content);
    }
}
