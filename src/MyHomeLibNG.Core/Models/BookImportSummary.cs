namespace MyHomeLibNG.Core.Models;

public sealed class BookImportSummary
{
    public string ScanPath { get; init; } = string.Empty;
    public int ArchivesScanned { get; init; }
    public int EntriesDiscovered { get; init; }
    public int ImportedCount { get; init; }
    public IReadOnlyList<BookImportFailure> Failures { get; init; } = Array.Empty<BookImportFailure>();
    public int FailedCount => Failures.Count;
}
