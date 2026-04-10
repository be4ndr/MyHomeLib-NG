namespace MyHomeLibNG.Core.Models;

public sealed class BookImportSummary
{
    public string ScanPath { get; init; } = string.Empty;
    public int ArchivesScanned { get; init; }
    public int EntriesDiscovered { get; init; }
    public int BooksAdded { get; init; }
    public int BooksUpdated { get; init; }
    public int BooksSkipped { get; init; }
    public int ImportedCount { get; init; }
    public IReadOnlyList<BookImportFailure> Failures { get; init; } = Array.Empty<BookImportFailure>();
    public int BooksFound => EntriesDiscovered;
    public int FailedCount => Failures.Count;
    public int ErrorsCount => Failures.Count;
}
