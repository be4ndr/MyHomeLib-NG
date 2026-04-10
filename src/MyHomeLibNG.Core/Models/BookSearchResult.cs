namespace MyHomeLibNG.Core.Models;

public sealed class BookSearchResult
{
    public string Title { get; init; } = string.Empty;
    public BookSourceDescriptor Source { get; init; } = new();
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? Series { get; init; }
    public string? Language { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Subjects { get; init; } = Array.Empty<string>();
    public string? Publisher { get; init; }
    public int? PublishedYear { get; init; }
    public string? Isbn10 { get; init; }
    public string? Isbn13 { get; init; }
    public string? CoverUrl { get; init; }
    public IReadOnlyList<string> Formats { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BookDownloadLink> DownloadLinks { get; init; } = Array.Empty<BookDownloadLink>();
    public string? ReadLink { get; init; }
    public string? BorrowLink { get; init; }
    public BookContentHandle? ContentHandle { get; init; }
}
