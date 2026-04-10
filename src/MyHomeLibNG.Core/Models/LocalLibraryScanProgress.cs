namespace MyHomeLibNG.Core.Models;

public sealed class LocalLibraryScanProgress
{
    public string? CurrentArchive { get; init; }
    public int ArchivesProcessed { get; init; }
    public int ArchivesTotal { get; init; }
    public int BooksFound { get; init; }
    public int BooksAdded { get; init; }
    public int BooksUpdated { get; init; }
    public int BooksSkipped { get; init; }
    public int ErrorsCount { get; init; }
    public IReadOnlyList<string> RecentLogLines { get; init; } = Array.Empty<string>();
    public bool IsCompleted { get; init; }
    public bool IsCancelled { get; init; }
}
