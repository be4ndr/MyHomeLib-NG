using System.Security.Cryptography;
using System.Threading.Channels;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class BookImportService
{
    private const int ParsedChannelCapacity = 24;
    private const int BatchSize = 250;
    private static readonly Fb2ParsingOptions ImportParsingOptions = Fb2ParsingOptions.FastImport;

    private readonly IZipArchiveScanner _zipArchiveScanner;
    private readonly IFb2MetadataParser _fb2MetadataParser;
    private readonly ILibraryRepository _libraryRepository;
    private readonly IInpxLibraryIndexReader? _inpxLibraryIndexReader;
    private readonly int _archiveWorkerCount;

    public BookImportService(
        IZipArchiveScanner zipArchiveScanner,
        IFb2MetadataParser fb2MetadataParser,
        ILibraryRepository libraryRepository,
        int? archiveWorkerCount = null,
        IInpxLibraryIndexReader? inpxLibraryIndexReader = null)
    {
        _zipArchiveScanner = zipArchiveScanner;
        _fb2MetadataParser = fb2MetadataParser;
        _libraryRepository = libraryRepository;
        _inpxLibraryIndexReader = inpxLibraryIndexReader;
        _archiveWorkerCount = archiveWorkerCount ?? Math.Clamp(Environment.ProcessorCount / 2, 2, 6);
    }

    public async Task<BookImportSummary> ImportLibraryAsync(
        LibraryProfile profile,
        string? inputPath = null,
        IProgress<BookImportProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Id <= 0)
        {
            throw new InvalidOperationException("Imported books require a persisted library profile.");
        }

        var scanPath = ResolveScanPath(profile, inputPath);
        var inpxSummary = await TryImportFromInpxAsync(profile, inputPath, scanPath, progress, cancellationToken);
        if (inpxSummary is not null)
        {
            return inpxSummary;
        }

        var archivePaths = _zipArchiveScanner
            .ResolveArchivePaths(scanPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var failures = new List<BookImportFailure>();
        var sync = new object();
        var entriesDiscovered = 0;
        var booksAdded = 0;
        var booksUpdated = 0;
        var booksSkipped = 0;
        var importedCount = 0;
        var errorsCount = 0;
        var archivesProcessed = 0;
        string? currentArchive = null;

        var parsedChannel = Channel.CreateBounded<ParsedBookResult>(new BoundedChannelOptions(ParsedChannelCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writerTask = Task.Run(async () =>
        {
            var batch = new List<BookImportRecord>(BatchSize);

            await foreach (var result in parsedChannel.Reader.ReadAllAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (result.ImportFailure is not null)
                {
                    lock (sync)
                    {
                        failures.Add(result.ImportFailure);
                        errorsCount++;
                    }

                    ReportProgress(progress, currentArchive, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
                        SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
                        $"Error in {result.ImportFailure.EntryPath}: {result.ImportFailure.Message}",
                        isImportant: true);
                    continue;
                }

                batch.Add(result.Record!);
                if (batch.Count >= BatchSize)
                {
                    await FlushBatchAsync(batch, cancellationToken);
                }
            }

            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, cancellationToken);
            }

            async Task FlushBatchAsync(List<BookImportRecord> records, CancellationToken token)
            {
                var batchResult = await WriteBatchWithFallbackAsync(records, failures, token);
                lock (sync)
                {
                    booksAdded += batchResult.BooksAdded;
                    booksUpdated += batchResult.BooksUpdated;
                    booksSkipped += batchResult.BooksSkipped;
                    importedCount += batchResult.BooksAdded + batchResult.BooksUpdated;
                    errorsCount = failures.Count;
                }

                ReportProgress(progress, currentArchive, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
                    SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
                    $"Indexed {records.Count} book{(records.Count == 1 ? string.Empty : "s")}.");
                records.Clear();
            }
        }, CancellationToken.None);

        var archiveTask = Task.Run(async () =>
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = _archiveWorkerCount
            };

            await Parallel.ForEachAsync(archivePaths, parallelOptions, async (archivePath, token) =>
            {
                lock (sync)
                {
                    currentArchive = archivePath;
                }

                ReportProgress(progress, archivePath, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
                    SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
                    $"Scanning {Path.GetFileName(archivePath)}", isImportant: true);

                await _zipArchiveScanner.ScanArchiveAsync(
                    archivePath,
                    async (entry, callbackToken) =>
                    {
                        lock (sync)
                        {
                            entriesDiscovered++;
                        }

                        try
                        {
                            var record = await BuildImportRecordAsync(profile.Id, entry, callbackToken);
                            await parsedChannel.Writer.WriteAsync(ParsedBookResult.Success(record), callbackToken);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            await parsedChannel.Writer.WriteAsync(
                                ParsedBookResult.Failure(new BookImportFailure
                                {
                                    ArchivePath = entry.ArchivePath,
                                    EntryPath = entry.EntryPath,
                                    Message = exception.Message
                                }),
                                callbackToken);
                        }
                    },
                    token);

                lock (sync)
                {
                    archivesProcessed++;
                }

                ReportProgress(progress, archivePath, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
                    SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
                    $"Finished {Path.GetFileName(archivePath)}");
            });
        }, CancellationToken.None);

        try
        {
            await archiveTask;
            parsedChannel.Writer.TryComplete();
            await writerTask;
        }
        catch
        {
            parsedChannel.Writer.TryComplete();
            throw;
        }

        ReportProgress(progress, currentArchive, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
            SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
            $"Completed scan: {importedCount} imported, {booksSkipped} skipped, {errorsCount} errors.",
            isImportant: true,
            isCompleted: true);

        return new BookImportSummary
        {
            ScanPath = scanPath,
            ArchivesScanned = archivePaths.Length,
            EntriesDiscovered = entriesDiscovered,
            BooksAdded = booksAdded,
            BooksUpdated = booksUpdated,
            BooksSkipped = booksSkipped,
            ImportedCount = importedCount,
            Failures = failures
        };

        int SnapshotEntriesDiscovered()
        {
            lock (sync)
            {
                return entriesDiscovered;
            }
        }

        int SnapshotBooksAdded()
        {
            lock (sync)
            {
                return booksAdded;
            }
        }

        int SnapshotBooksUpdated()
        {
            lock (sync)
            {
                return booksUpdated;
            }
        }

        int SnapshotBooksSkipped()
        {
            lock (sync)
            {
                return booksSkipped;
            }
        }

        int SnapshotErrorsCount()
        {
            lock (sync)
            {
                return errorsCount;
            }
        }

        int SnapshotArchivesProcessed()
        {
            lock (sync)
            {
                return archivesProcessed;
            }
        }
    }

    private async Task<BookImportBatchResult> WriteBatchWithFallbackAsync(
        IReadOnlyList<BookImportRecord> records,
        List<BookImportFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _libraryRepository.UpsertImportedBooksAsync(records, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (records.Count == 1)
            {
                failures.Add(new BookImportFailure
                {
                    ArchivePath = records[0].ArchivePath,
                    EntryPath = records[0].EntryPath,
                    Message = exception.Message
                });

                return new BookImportBatchResult();
            }
        }

        var fallbackAdded = 0;
        var fallbackUpdated = 0;
        var fallbackSkipped = 0;

        foreach (var record in records)
        {
            try
            {
                var singleResult = await _libraryRepository.UpsertImportedBooksAsync([record], cancellationToken);
                fallbackAdded += singleResult.BooksAdded;
                fallbackUpdated += singleResult.BooksUpdated;
                fallbackSkipped += singleResult.BooksSkipped;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new BookImportFailure
                {
                    ArchivePath = record.ArchivePath,
                    EntryPath = record.EntryPath,
                    Message = exception.Message
                });
            }
        }

        return new BookImportBatchResult
        {
            BooksAdded = fallbackAdded,
            BooksUpdated = fallbackUpdated,
            BooksSkipped = fallbackSkipped
        };
    }

    private async Task<BookImportSummary?> TryImportFromInpxAsync(
        LibraryProfile profile,
        string? inputPath,
        string scanPath,
        IProgress<BookImportProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        if (_inpxLibraryIndexReader is null || !ShouldUseInpxPrimary(profile, inputPath))
        {
            return null;
        }

        var inpxPath = profile.FolderSource!.InpxFilePath;
        ReportProgress(progress, inpxPath, 0, 0, 0, 0, 0, 0, "Reading INPX catalog...", isImportant: true);

        var records = await _inpxLibraryIndexReader.ReadImportRecordsAsync(profile, cancellationToken);
        if (records.Count == 0)
        {
            ReportProgress(progress, inpxPath, 0, 0, 0, 0, 0, 0, "INPX catalog had no importable records. Falling back to ZIP/FB2 scan.", isImportant: true);
            return null;
        }

        var failures = new List<BookImportFailure>();
        var booksAdded = 0;
        var booksUpdated = 0;
        var booksSkipped = 0;
        const int inpxBatchSize = BatchSize;

        for (var offset = 0; offset < records.Count; offset += inpxBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var count = Math.Min(inpxBatchSize, records.Count - offset);
            var batch = new BookImportRecord[count];
            for (var index = 0; index < count; index++)
            {
                batch[index] = records[offset + index];
            }
            var batchResult = await WriteBatchWithFallbackAsync(batch, failures, cancellationToken);
            booksAdded += batchResult.BooksAdded;
            booksUpdated += batchResult.BooksUpdated;
            booksSkipped += batchResult.BooksSkipped;

            ReportProgress(
                progress,
                inpxPath,
                0,
                records.Count,
                booksAdded,
                booksUpdated,
                booksSkipped,
                failures.Count,
                $"Indexed {Math.Min(offset + count, records.Count)} of {records.Count} INPX records.");
        }

        var importedCount = booksAdded + booksUpdated;
        ReportProgress(
            progress,
            inpxPath,
            0,
            records.Count,
            booksAdded,
            booksUpdated,
            booksSkipped,
            failures.Count,
            $"Completed INPX index import: {importedCount} imported, {booksSkipped} skipped, {failures.Count} errors.",
            isImportant: true,
            isCompleted: true);

        return new BookImportSummary
        {
            ScanPath = scanPath,
            ArchivesScanned = records
                .Select(record => record.ArchivePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            EntriesDiscovered = records.Count,
            BooksAdded = booksAdded,
            BooksUpdated = booksUpdated,
            BooksSkipped = booksSkipped,
            ImportedCount = importedCount,
            Failures = failures
        };
    }

    private async Task<BookImportRecord> BuildImportRecordAsync(
        long libraryProfileId,
        ZipArchiveBookEntry entry,
        CancellationToken cancellationToken)
    {
        await using var stream = await entry.OpenReadAsync(cancellationToken);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var hashingStream = new HashingReadStream(stream, hash);
        var metadata = await _fb2MetadataParser.ParseAsync(hashingStream, ImportParsingOptions, cancellationToken);
        var timestamp = DateTimeOffset.UtcNow;

        return new BookImportRecord
        {
            LibraryProfileId = libraryProfileId,
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? entry.FileName : metadata.Title.Trim(),
            Authors = JoinValues(metadata.Authors),
            Annotation = NullIfWhiteSpace(metadata.Annotation),
            PublishYear = metadata.PublishYear,
            PrimaryFormat = FileFormat.Fb2,
            Series = NullIfWhiteSpace(metadata.Series),
            SeriesNumber = metadata.SeriesNumber,
            Genres = JoinValues(metadata.Genres),
            Language = NullIfWhiteSpace(metadata.Language),
            ArchivePath = entry.ArchivePath,
            EntryPath = entry.EntryPath,
            FileName = entry.FileName,
            FileSize = entry.FileSize,
            ContentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            CoverThumbnail = metadata.ThumbnailBytes,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static string ResolveScanPath(LibraryProfile profile, string? inputPath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            return inputPath.Trim();
        }

        var archiveDirectoryPath = profile.FolderSource?.ArchiveDirectoryPath;
        if (!string.IsNullOrWhiteSpace(archiveDirectoryPath))
        {
            return archiveDirectoryPath;
        }

        throw new InvalidOperationException("Folder library profiles require an archive directory path to import books.");
    }

    private static bool ShouldUseInpxPrimary(LibraryProfile profile, string? inputPath)
    {
        if (profile.LibraryType != LibraryType.Folder || string.IsNullOrWhiteSpace(profile.FolderSource?.InpxFilePath))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(inputPath) ||
               !inputPath.Trim().EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string? JoinValues(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        HashSet<string>? unique = null;
        List<string>? ordered = null;

        foreach (var value in values)
        {
            var normalized = NullIfWhiteSpace(value);
            if (normalized is null)
            {
                continue;
            }

            unique ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!unique.Add(normalized))
            {
                continue;
            }

            ordered ??= new List<string>();
            ordered.Add(normalized);
        }

        return ordered is null || ordered.Count == 0
            ? null
            : string.Join("; ", ordered);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ReportProgress(
        IProgress<BookImportProgressUpdate>? progress,
        string? currentArchive,
        int archivesProcessed,
        int booksFound,
        int booksAdded,
        int booksUpdated,
        int booksSkipped,
        int errorsCount,
        string? logLine,
        bool isImportant = false,
        bool isCompleted = false)
    {
        progress?.Report(new BookImportProgressUpdate
        {
            CurrentArchive = currentArchive,
            ArchivesProcessed = archivesProcessed,
            BooksFound = booksFound,
            BooksAdded = booksAdded,
            BooksUpdated = booksUpdated,
            BooksSkipped = booksSkipped,
            ErrorsCount = errorsCount,
            LogLine = logLine,
            IsImportant = isImportant,
            IsCompleted = isCompleted
        });
    }

    private sealed class ParsedBookResult
    {
        public BookImportRecord? Record { get; private init; }
        public BookImportFailure? ImportFailure { get; private init; }

        public static ParsedBookResult Success(BookImportRecord record)
            => new() { Record = record };

        public static ParsedBookResult Failure(BookImportFailure importFailure)
            => new() { ImportFailure = importFailure };
    }

    private sealed class HashingReadStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly IncrementalHash _hash;

        public HashingReadStream(Stream innerStream, IncrementalHash hash)
        {
            _innerStream = innerStream;
            _hash = hash;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush()
            => _innerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = _innerStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                _hash.AppendData(buffer, offset, bytesRead);
            }

            return bytesRead;
        }

        public override int Read(Span<byte> buffer)
        {
            var bytesRead = _innerStream.Read(buffer);
            if (bytesRead > 0)
            {
                _hash.AppendData(buffer[..bytesRead]);
            }

            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead > 0)
            {
                _hash.AppendData(buffer[..bytesRead].Span);
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => _innerStream.Seek(offset, origin);

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new NotSupportedException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
