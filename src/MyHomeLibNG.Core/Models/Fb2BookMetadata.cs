namespace MyHomeLibNG.Core.Models;

public sealed class Fb2BookMetadata
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();
    public string? Annotation { get; init; }
    public string? Series { get; init; }
    public int? SeriesNumber { get; init; }
    public string? Language { get; init; }
    public int? PublishYear { get; init; }
    public string? CoverReference { get; init; }
    public byte[]? CoverImageBytes { get; init; }
    public byte[]? ThumbnailBytes { get; init; }
}
