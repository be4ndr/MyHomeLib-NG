using System.Text;
using MyHomeLibNG.Infrastructure.Import;
using MyHomeLibNG.Infrastructure.Providers.Offline;
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

    [Fact]
    public async Task ScanArchiveAsync_OpensArchiveOnceWhileProcessingEntries()
    {
        const string archivePath = "/library/archives/books.zip";
        var fileSystem = new CountingOfflineLibraryFileSystem();
        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("nested/one.fb2", Fb2ImportTestData.CreateFb2(title: "One")),
            ("nested/two.fb2", Fb2ImportTestData.CreateFb2(title: "Two"))));

        var scanner = new ZipArchiveScanner(fileSystem);
        var entriesProcessed = 0;

        await scanner.ScanArchiveAsync(
            archivePath,
            async (entry, token) =>
            {
                await using var stream = await entry.OpenReadAsync(token);
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
                var content = await reader.ReadToEndAsync(token);
                Assert.Contains("FictionBook", content);
                entriesProcessed++;
            });

        Assert.Equal(2, entriesProcessed);
        Assert.Equal(1, fileSystem.GetOpenCount(archivePath));
    }

    private sealed class CountingOfflineLibraryFileSystem : IOfflineLibraryFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _openCounts = new(StringComparer.OrdinalIgnoreCase);

        public void AddFile(string path, byte[] content)
        {
            _files[path] = content;
        }

        public bool FileExists(string path)
            => _files.ContainsKey(path);

        public OfflineFileMetadata? GetMetadata(string path)
        {
            if (!_files.TryGetValue(path, out var content))
            {
                return null;
            }

            return new OfflineFileMetadata
            {
                LengthBytes = content.LongLength,
                LastWriteTimeUtc = DateTimeOffset.UtcNow
            };
        }

        public IReadOnlyList<string> EnumerateFilesRecursive(string rootPath)
        {
            return _files.Keys
                .Where(path => path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            _openCounts[path] = GetOpenCount(path) + 1;

            if (!_files.TryGetValue(path, out var content))
            {
                throw new FileNotFoundException(path);
            }

            Stream stream = new MemoryStream(content, writable: false);
            return Task.FromResult(stream);
        }

        public int GetOpenCount(string path)
            => _openCounts.TryGetValue(path, out var count) ? count : 0;
    }
}
