using System.Collections.ObjectModel;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class ScanProgressWindowViewModel : ObservableObject, IDisposable
{
    private readonly LocalLibraryScanCoordinator _scanCoordinator;
    private readonly LibraryProfile _profile;
    private LocalLibraryScanOperation? _operation;
    private bool _hasStarted;
    private bool _isRunning;
    private bool _isCompleted;
    private bool _isCancelled;
    private string? _currentArchive;
    private int _archivesProcessed;
    private int _archivesTotal;
    private int _booksFound;
    private int _booksAdded;
    private int _booksUpdated;
    private int _booksSkipped;
    private int _errorsCount;
    private string _statusMessage = "Preparing scan...";

    public ScanProgressWindowViewModel(
        LocalLibraryScanCoordinator scanCoordinator,
        LibraryProfile profile)
    {
        _scanCoordinator = scanCoordinator;
        _profile = profile;
    }

    public string LibraryName => _profile.Name;

    public string CurrentArchive
    {
        get => _currentArchive ?? "Waiting for the first archive...";
        private set => SetProperty(ref _currentArchive, value);
    }

    public int ArchivesProcessed
    {
        get => _archivesProcessed;
        private set
        {
            if (SetProperty(ref _archivesProcessed, value))
            {
                OnPropertyChanged(nameof(ProgressMaximum));
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public int ArchivesTotal
    {
        get => _archivesTotal;
        private set
        {
            if (SetProperty(ref _archivesTotal, value))
            {
                OnPropertyChanged(nameof(ProgressMaximum));
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public int BooksFound
    {
        get => _booksFound;
        private set => SetProperty(ref _booksFound, value);
    }

    public int BooksAdded
    {
        get => _booksAdded;
        private set => SetProperty(ref _booksAdded, value);
    }

    public int BooksUpdated
    {
        get => _booksUpdated;
        private set => SetProperty(ref _booksUpdated, value);
    }

    public int BooksSkipped
    {
        get => _booksSkipped;
        private set => SetProperty(ref _booksSkipped, value);
    }

    public int ErrorsCount
    {
        get => _errorsCount;
        private set => SetProperty(ref _errorsCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanClose));
                OnPropertyChanged(nameof(ShowActivityBar));
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                OnPropertyChanged(nameof(CanClose));
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        private set
        {
            if (SetProperty(ref _isCancelled, value))
            {
                OnPropertyChanged(nameof(CanClose));
                OnPropertyChanged(nameof(ProgressLabel));
            }
        }
    }

    public ObservableCollection<string> RecentLogLines { get; } = [];
    public double ProgressMaximum => Math.Max(1, ArchivesTotal);
    public double ProgressValue => Math.Min(ArchivesProcessed, ProgressMaximum);
    public string ProgressLabel => ArchivesTotal > 0
        ? $"{ArchivesProcessed} of {ArchivesTotal} archives processed"
        : IsCompleted
            ? $"{ArchivesProcessed} archives processed"
            : "Counting archives...";
    public bool CanCancel => IsRunning;
    public bool CanClose => !IsRunning;
    public bool ShowActivityBar => IsRunning;

    public Task StartAsync()
    {
        if (_hasStarted)
        {
            return Task.CompletedTask;
        }

        _hasStarted = true;
        IsRunning = true;
        StatusMessage = $"Scanning {_profile.Name} in the background...";

        var progress = new Progress<LocalLibraryScanProgress>(ApplyProgress);
        _operation = _scanCoordinator.StartScan(_profile, progress: progress);
        _ = ObserveCompletionAsync(_operation);
        return Task.CompletedTask;
    }

    public void Cancel()
    {
        if (!CanCancel || _operation is null)
        {
            return;
        }

        StatusMessage = "Cancelling scan...";
        _operation.Cancel();
    }

    public void Dispose()
    {
        if (_operation?.Completion.IsCompleted == true)
        {
            _operation.Dispose();
        }
    }

    private async Task ObserveCompletionAsync(LocalLibraryScanOperation operation)
    {
        try
        {
            var summary = await operation.Completion;
            StatusMessage = $"Scan complete. Added {summary.BooksAdded}, updated {summary.BooksUpdated}, skipped {summary.BooksSkipped}.";
        }
        catch (OperationCanceledException)
        {
            IsCancelled = true;
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception exception)
        {
            ErrorsCount++;
            PrependLogLine($"Fatal error: {exception.Message}");
            StatusMessage = "Scan stopped because of an unexpected error.";
        }
        finally
        {
            IsRunning = false;
            IsCompleted = !IsCancelled;
        }
    }

    private void ApplyProgress(LocalLibraryScanProgress progress)
    {
        CurrentArchive = progress.CurrentArchive ?? "Waiting for the first archive...";
        ArchivesProcessed = progress.ArchivesProcessed;
        ArchivesTotal = progress.ArchivesTotal;
        BooksFound = progress.BooksFound;
        BooksAdded = progress.BooksAdded;
        BooksUpdated = progress.BooksUpdated;
        BooksSkipped = progress.BooksSkipped;
        ErrorsCount = progress.ErrorsCount;

        RecentLogLines.Clear();
        foreach (var line in progress.RecentLogLines.Reverse())
        {
            RecentLogLines.Add(line);
        }

        if (progress.IsCancelled)
        {
            IsCancelled = true;
        }

        if (progress.IsCompleted)
        {
            IsCompleted = true;
        }
    }

    private void PrependLogLine(string line)
    {
        RecentLogLines.Insert(0, line);
        while (RecentLogLines.Count > 10)
        {
            RecentLogLines.RemoveAt(RecentLogLines.Count - 1);
        }
    }
}
