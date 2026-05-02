using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class LocalLibraryScanCoordinator
{
    private static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromMilliseconds(250);
    private readonly BookImportService _bookImportService;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _progressInterval;
    private readonly object _sync = new();
    private readonly HashSet<long> _activeLibraryIds = [];

    public LocalLibraryScanCoordinator(
        BookImportService bookImportService,
        TimeProvider? timeProvider = null,
        TimeSpan? progressInterval = null)
    {
        _bookImportService = bookImportService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _progressInterval = progressInterval ?? DefaultProgressInterval;
    }

    public LocalLibraryScanOperation StartScan(
        LibraryProfile profile,
        string? inputPath = null,
        IProgress<LocalLibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Id <= 0)
        {
            throw new InvalidOperationException("Only persisted libraries can be scanned.");
        }

        lock (_sync)
        {
            if (!_activeLibraryIds.Add(profile.Id))
            {
                throw new InvalidOperationException($"A scan is already running for '{profile.Name}'.");
            }
        }

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var scanPath = ResolveScanPath(profile, inputPath);
        var progressState = new ProgressState(progress, _timeProvider, _progressInterval);
        progressState.ReportInitial();

        var completion = Task.Run(async () =>
        {
            try
            {
                progressState.SetArchivesTotal(CountArchives(scanPath));
                var importProgress = new Progress<BookImportProgressUpdate>(progressState.HandleUpdate);
                var summary = await _bookImportService.ImportLibraryAsync(
                    profile,
                    inputPath,
                    importProgress,
                    linkedCancellation.Token);

                progressState.ReportCompleted(summary);
                return summary;
            }
            catch (OperationCanceledException)
            {
                progressState.ReportCancelled();
                throw;
            }
            finally
            {
                lock (_sync)
                {
                    _activeLibraryIds.Remove(profile.Id);
                }
            }
        }, CancellationToken.None);

        return new LocalLibraryScanOperation(profile.Id, completion, linkedCancellation);
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

        throw new InvalidOperationException("Folder library profiles require an archive directory path to scan books.");
    }

    private static int CountArchives(string path)
    {
        if (File.Exists(path))
        {
            return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        if (!Directory.Exists(path))
        {
            return 0;
        }

        return Directory
            .EnumerateFiles(path, "*.zip", SearchOption.AllDirectories)
            .Count();
    }

    private sealed class ProgressState
    {
        private readonly object _sync = new();
        private readonly IProgress<LocalLibraryScanProgress>? _progress;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _progressInterval;
        private readonly Queue<string> _recentLogLines = new();
        private DateTimeOffset _lastReportedAt = DateTimeOffset.MinValue;
        private int _archivesTotal;
        private string? _currentArchive;
        private int _archivesProcessed;
        private int _booksFound;
        private int _booksAdded;
        private int _booksUpdated;
        private int _booksSkipped;
        private int _errorsCount;

        public ProgressState(
            IProgress<LocalLibraryScanProgress>? progress,
            TimeProvider timeProvider,
            TimeSpan progressInterval)
        {
            _progress = progress;
            _timeProvider = timeProvider;
            _progressInterval = progressInterval;
        }

        public int ArchivesTotal => _archivesTotal;

        public void SetArchivesTotal(int value)
        {
            lock (_sync)
            {
                _archivesTotal = value;
                EnqueueLogLine($"Discovered {ArchivesTotal} archive{(ArchivesTotal == 1 ? string.Empty : "s")} to scan.");
            }

            Report(force: true);
        }

        public void ReportInitial()
        {
            lock (_sync)
            {
                EnqueueLogLine("Queued scan.");
            }

            Report(force: true);
        }

        public void HandleUpdate(BookImportProgressUpdate update)
        {
            lock (_sync)
            {
                _currentArchive = update.CurrentArchive;
                _archivesProcessed = update.ArchivesProcessed;
                _booksFound = update.BooksFound;
                _booksAdded = update.BooksAdded;
                _booksUpdated = update.BooksUpdated;
                _booksSkipped = update.BooksSkipped;
                _errorsCount = update.ErrorsCount;

                if (!string.IsNullOrWhiteSpace(update.LogLine))
                {
                    EnqueueLogLine(update.LogLine!);
                }
            }

            Report(update.IsImportant || update.IsCompleted);
        }

        public void ReportCompleted(BookImportSummary summary)
        {
            lock (_sync)
            {
                _archivesProcessed = Math.Max(_archivesProcessed, ArchivesTotal > 0 ? ArchivesTotal : summary.ArchivesScanned);
                _booksFound = summary.BooksFound;
                _booksAdded = summary.BooksAdded;
                _booksUpdated = summary.BooksUpdated;
                _booksSkipped = summary.BooksSkipped;
                _errorsCount = summary.ErrorsCount;
                EnqueueLogLine($"Scan finished. Added {_booksAdded}, updated {_booksUpdated}, skipped {_booksSkipped}.");
            }

            Report(force: true, isCompleted: true);
        }

        public void ReportCancelled()
        {
            lock (_sync)
            {
                EnqueueLogLine("Scan cancelled.");
            }

            Report(force: true, isCancelled: true);
        }

        private void Report(bool force = false, bool isCompleted = false, bool isCancelled = false)
        {
            if (_progress is null)
            {
                return;
            }

            LocalLibraryScanProgress snapshot;
            lock (_sync)
            {
                var now = _timeProvider.GetUtcNow();
                if (!force &&
                    _lastReportedAt != DateTimeOffset.MinValue &&
                    now - _lastReportedAt < _progressInterval)
                {
                    return;
                }

                _lastReportedAt = now;
                snapshot = new LocalLibraryScanProgress
                {
                    CurrentArchive = string.IsNullOrWhiteSpace(_currentArchive) ? null : Path.GetFileName(_currentArchive),
                    ArchivesProcessed = _archivesProcessed,
                    ArchivesTotal = ArchivesTotal,
                    BooksFound = _booksFound,
                    BooksAdded = _booksAdded,
                    BooksUpdated = _booksUpdated,
                    BooksSkipped = _booksSkipped,
                    ErrorsCount = _errorsCount,
                    RecentLogLines = _recentLogLines.ToArray(),
                    IsCompleted = isCompleted,
                    IsCancelled = isCancelled
                };
            }

            _progress.Report(snapshot);
        }

        private void EnqueueLogLine(string value)
        {
            _recentLogLines.Enqueue(value);
            while (_recentLogLines.Count > 10)
            {
                _recentLogLines.Dequeue();
            }
        }
    }
}
