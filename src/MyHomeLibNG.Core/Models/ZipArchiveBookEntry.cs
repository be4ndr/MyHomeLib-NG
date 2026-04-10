namespace MyHomeLibNG.Core.Models;

public sealed class ZipArchiveBookEntry
{
    public string ArchivePath { get; init; } = string.Empty;
    public string EntryPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public Func<CancellationToken, Task<Stream>> OpenReadAsync { get; init; } =
        _ => throw new InvalidOperationException("No entry stream factory was provided.");
}
