namespace MyHomeLibNG.Core.Models;

public sealed class BookImportFailure
{
    public string ArchivePath { get; init; } = string.Empty;
    public string EntryPath { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
