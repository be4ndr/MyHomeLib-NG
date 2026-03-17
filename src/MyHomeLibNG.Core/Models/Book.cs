using MyHomeLibNext.Core.Enums;

namespace MyHomeLibNext.Core.Models;

public sealed class Book
{
    public long Id { get; init; }
    public long LibraryProfileId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Annotation { get; init; }
    public int? PublishYear { get; init; }
    public FileFormat PrimaryFormat { get; init; }
    public IReadOnlyList<Author> Authors { get; init; } = Array.Empty<Author>();
    public IReadOnlyList<BookSource> Sources { get; init; } = Array.Empty<BookSource>();
}
