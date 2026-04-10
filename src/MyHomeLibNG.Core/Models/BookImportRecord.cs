using MyHomeLibNG.Core.Enums;

namespace MyHomeLibNG.Core.Models;

public sealed class BookImportRecord
{
    public long LibraryProfileId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Authors { get; init; }
    public string? Annotation { get; init; }
    public int? PublishYear { get; init; }
    public FileFormat PrimaryFormat { get; init; } = FileFormat.Fb2;
    public string? Series { get; init; }
    public int? SeriesNumber { get; init; }
    public string? Genres { get; init; }
    public string? Language { get; init; }
    public string ArchivePath { get; init; } = string.Empty;
    public string EntryPath { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? ContentHash { get; init; }
    public byte[]? CoverThumbnail { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
