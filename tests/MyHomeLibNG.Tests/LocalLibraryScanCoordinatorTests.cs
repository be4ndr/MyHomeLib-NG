using System.Collections.Concurrent;
using System.Text;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class LocalLibraryScanCoordinatorTests
{
    [Fact]
    public async Task StartScan_ReportsProgressInThrottledSnapshots()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.CreateFile("library.zip");
        var scanner = new FakeZipArchiveScanner(
        [
            CreateEntry(archivePath, "books/one.fb2", "<fb2/>"),
            CreateEntry(archivePath, "books/two.fb2", "<fb2/>"),
            CreateEntry(archivePath, "books/three.fb2", "<fb2/>"),
            CreateEntry(archivePath, "books/four.fb2", "<fb2/>")
        ]);

        var coordinator = new LocalLibraryScanCoordinator(
            new BookImportService(scanner, new FakeFb2MetadataParser(), new RecordingLibraryRepository()),
            progressInterval: TimeSpan.FromDays(1));

        var progressSnapshots = new List<LocalLibraryScanProgress>();
        using var operation = coordinator.StartScan(
            CreateProfile(),
            workspace.DirectoryPath,
            progress: new ImmediateProgress<LocalLibraryScanProgress>(snapshot => progressSnapshots.Add(snapshot)));

        var summary = await operation.Completion;

        Assert.Equal(4, summary.BooksAdded);
        Assert.Equal(0, summary.BooksUpdated);
        Assert.Equal(0, summary.BooksSkipped);
        Assert.NotEmpty(progressSnapshots);
        Assert.True(progressSnapshots.Count < 7);

        var finalSnapshot = progressSnapshots[^1];
        Assert.True(finalSnapshot.IsCompleted);
        Assert.Equal(1, finalSnapshot.ArchivesProcessed);
        Assert.Equal(1, finalSnapshot.ArchivesTotal);
        Assert.Equal(4, finalSnapshot.BooksFound);
        Assert.Equal(4, finalSnapshot.BooksAdded);
    }

    [Fact]
    public async Task StartScan_CanBeCancelled()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.CreateFile("library.zip");
        var scanner = new FakeZipArchiveScanner(
        [
            new ZipArchiveBookEntry
            {
                ArchivePath = archivePath,
                EntryPath = "books/blocking.fb2",
                FileName = "blocking.fb2",
                FileSize = 1,
                OpenReadAsync = _ => Task.FromResult<Stream>(new BlockingReadStream())
            }
        ]);

        var progressSnapshots = new ConcurrentQueue<LocalLibraryScanProgress>();
        var coordinator = new LocalLibraryScanCoordinator(
            new BookImportService(scanner, new FakeFb2MetadataParser(), new RecordingLibraryRepository()));

        using var operation = coordinator.StartScan(
            CreateProfile(),
            workspace.DirectoryPath,
            progress: new ImmediateProgress<LocalLibraryScanProgress>(snapshot => progressSnapshots.Enqueue(snapshot)));

        operation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completion);
        Assert.True(progressSnapshots.Last().IsCancelled);
    }

    [Fact]
    public async Task StartScan_StartsBackgroundWorkWithoutBlockingCaller()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.CreateFile("library.zip");
        var scanner = new FakeZipArchiveScanner(
        [
            new ZipArchiveBookEntry
            {
                ArchivePath = archivePath,
                EntryPath = "books/blocking.fb2",
                FileName = "blocking.fb2",
                FileSize = 1,
                OpenReadAsync = _ => Task.FromResult<Stream>(new BlockingReadStream())
            }
        ]);

        var coordinator = new LocalLibraryScanCoordinator(
            new BookImportService(scanner, new FakeFb2MetadataParser(), new RecordingLibraryRepository()));

        var startedAt = DateTimeOffset.UtcNow;
        using var operation = coordinator.StartScan(CreateProfile(), workspace.DirectoryPath);
        var elapsed = DateTimeOffset.UtcNow - startedAt;

        Assert.True(elapsed < TimeSpan.FromMilliseconds(200));
        Assert.False(operation.Completion.IsCompleted);

        operation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completion);
    }

    private static ZipArchiveBookEntry CreateEntry(string archivePath, string entryPath, string content)
    {
        return new ZipArchiveBookEntry
        {
            ArchivePath = archivePath,
            EntryPath = entryPath,
            FileName = Path.GetFileName(entryPath),
            FileSize = content.Length,
            OpenReadAsync = _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content), writable: false))
        };
    }

    private static LibraryProfile CreateProfile()
    {
        return new LibraryProfile
        {
            Id = 501,
            Name = "Background scan test",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = "/library/catalog.inpx",
                ArchiveDirectoryPath = "/library/archives"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeZipArchiveScanner : IZipArchiveScanner
    {
        private readonly IReadOnlyList<ZipArchiveBookEntry> _entries;

        public FakeZipArchiveScanner(IReadOnlyList<ZipArchiveBookEntry> entries)
        {
            _entries = entries;
        }

        public async IAsyncEnumerable<ZipArchiveBookEntry> ScanAsync(
            string path,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var entry in _entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entry;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeFb2MetadataParser : IFb2MetadataParser
    {
        private int _counter;

        public Task<Fb2BookMetadata> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _counter);
            return Task.FromResult(new Fb2BookMetadata
            {
                Title = $"Book {index}",
                Authors = [$"Author {index}"],
                Genres = ["test"],
                Annotation = $"Annotation {index}",
                Language = "en",
                PublishYear = 2024
            });
        }
    }

    private sealed class RecordingLibraryRepository : ILibraryRepository
    {
        private readonly Dictionary<(long LibraryId, string ArchivePath, string EntryPath), ImportedBookMetadataSnapshot> _books = new();
        private long _nextId = 1;

        public Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
            long libraryProfileId,
            string archivePath,
            string entryPath,
            CancellationToken cancellationToken = default)
        {
            _books.TryGetValue((libraryProfileId, archivePath, entryPath), out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default)
        {
            var key = (book.LibraryProfileId, book.ArchivePath, book.EntryPath);
            _books[key] = new ImportedBookMetadataSnapshot
            {
                Title = book.Title,
                Authors = book.Authors,
                Annotation = book.Annotation,
                PublishYear = book.PublishYear,
                Series = book.Series,
                Genres = book.Genres,
                Language = book.Language,
                ContentHash = book.ContentHash,
                CoverThumbnail = book.CoverThumbnail
            };

            return Task.FromResult(_nextId++);
        }

        public Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<int>(_ => 0, cancellationToken));
    }

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public ImmediateProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-scan-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string CreateFile(string fileName)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            File.WriteAllBytes(path, [1, 2, 3]);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
