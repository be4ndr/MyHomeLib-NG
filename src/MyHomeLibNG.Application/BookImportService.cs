using System.Security.Cryptography;
using System.Threading.Channels;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class BookImportService
{
    private const int EntryChannelCapacity = 24;
    private const int ParsedChannelCapacity = 24;
    private const int BatchSize = 250;
    private static readonly Fb2ParsingOptions ImportParsingOptions = Fb2ParsingOptions.FastImport;

    private readonly IZipArchiveScanner _zipArchiveScanner;
    private readonly IFb2MetadataParser _fb2MetadataParser;
    private readonly ILibraryRepository _libraryRepository;
    private readonly int _parserWorkerCount;

    public BookImportService(
        IZipArchiveScanner zipArchiveScanner,
        IFb2MetadataParser fb2MetadataParser,
        ILibraryRepository libraryRepository,
        int? parserWorkerCount = null)
    {
        _zipArchiveScanner = zipArchiveScanner;
        _fb2MetadataParser = fb2MetadataParser;
        _libraryRepository = libraryRepository;
        _parserWorkerCount = parserWorkerCount ?? Math.Clamp(Environment.ProcessorCount / 2, 2, 4);
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
        var failures = new List<BookImportFailure>();
        var archivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sync = new object();
        var entriesDiscovered = 0;
        var booksAdded = 0;
        var booksUpdated = 0;
        var booksSkipped = 0;
        var importedCount = 0;
        var errorsCount = 0;
        var archivesProcessed = 0;
        string? currentArchive = null;

        var entryChannel = Channel.CreateBounded<ZipArchiveBookEntry>(new BoundedChannelOptions(EntryChannelCapacity)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        var parsedChannel = Channel.CreateBounded<ParsedBookResult>(new BoundedChannelOptions(ParsedChannelCapacity)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var entry in _zipArchiveScanner.ScanAsync(scanPath, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool archiveChanged;
                    lock (sync)
                    {
                        archiveChanged = !string.Equals(currentArchive, entry.ArchivePath, StringComparison.OrdinalIgnoreCase);
                        if (archiveChanged)
                        {
                            if (currentArchive is not null)
                            {
                                archivesProcessed++;
                            }

                            currentArchive = entry.ArchivePath;
                            archivePaths.Add(entry.ArchivePath);
                        }

                        entriesDiscovered++;
                    }

                    if (archiveChanged)
                    {
                        ReportProgress(progress, currentArchive, SnapshotArchivesProcessed(), SnapshotEntriesDiscovered(),
                            SnapshotBooksAdded(), SnapshotBooksUpdated(), SnapshotBooksSkipped(), SnapshotErrorsCount(),
                            $"Scanning {Path.GetFileName(currentArchive)}", isImportant: true);
                    }

                    await entryChannel.Writer.WriteAsync(entry, cancellationToken);
                }
            }
            finally
            {
                lock (sync)
                {
                    if (currentArchive is not null)
                    {
                        archivesProcessed = archivePaths.Count;
                    }
                }

                entryChannel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        var parserTasks = Enumerable.Range(0, _parserWorkerCount)
            .Select(_ => Task.Run(async () =>
            {
                await foreach (var entry in entryChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        var record = await BuildImportRecordAsync(profile.Id, entry, cancellationToken);
                        await parsedChannel.Writer.WriteAsync(ParsedBookResult.Success(record), cancellationToken);
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
                            cancellationToken);
                    }
                }
            }, CancellationToken.None))
            .ToArray();

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

        try
        {
            await producerTask;
            await Task.WhenAll(parserTasks);
            parsedChannel.Writer.TryComplete();
            await writerTask;
        }
        catch
        {
            entryChannel.Writer.TryComplete();
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
            ArchivesScanned = archivePaths.Count,
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

    private static string? JoinValues(IReadOnlyList<string> values)
    {
        var normalized = values
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join("; ", normalized);
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
