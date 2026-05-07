namespace MyHomeLibNG.Core.Models;

public sealed class ImportedBookMetadataSnapshot
{
    public string Title { get; init; } = string.Empty;
    public string? Authors { get; init; }
    public string? Annotation { get; init; }
    public int? PublishYear { get; init; }
    public string? Series { get; init; }
    public int? SeriesNumber { get; init; }
    public string? Genres { get; init; }
    public string? Language { get; init; }
    public string ArchivePath { get; init; } = string.Empty;
    public string EntryPath { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? LibId { get; init; }
    public string? ContentHash { get; init; }
    public byte[]? CoverThumbnail { get; init; }
}
