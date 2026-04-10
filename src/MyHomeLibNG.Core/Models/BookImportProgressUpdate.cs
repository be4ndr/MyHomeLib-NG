namespace MyHomeLibNG.Core.Models;

public sealed class BookImportProgressUpdate
{
    public string? CurrentArchive { get; init; }
    public int ArchivesProcessed { get; init; }
    public int BooksFound { get; init; }
    public int BooksAdded { get; init; }
    public int BooksUpdated { get; init; }
    public int BooksSkipped { get; init; }
    public int ErrorsCount { get; init; }
    public string? LogLine { get; init; }
    public bool IsImportant { get; init; }
    public bool IsCompleted { get; init; }
}
