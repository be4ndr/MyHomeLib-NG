using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class LocalLibraryScanOperation : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    public LocalLibraryScanOperation(
        long libraryProfileId,
        Task<BookImportSummary> completion,
        CancellationTokenSource cancellationTokenSource)
    {
        LibraryProfileId = libraryProfileId;
        Completion = completion;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public long LibraryProfileId { get; }
    public Task<BookImportSummary> Completion { get; }
    public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

    public void Cancel()
        => _cancellationTokenSource.Cancel();

    public void Dispose()
        => _cancellationTokenSource.Dispose();
}
