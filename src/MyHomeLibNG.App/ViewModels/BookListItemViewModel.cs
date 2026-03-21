using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class BookListItemViewModel
{
    public BookListItemViewModel(BookSearchResult book)
    {
        Book = book;
    }

    public BookSearchResult Book { get; }
    public string Title => Book.Title;
    public string AuthorsLine => Book.Authors.Count == 0 ? "Unknown author" : string.Join(", ", Book.Authors);

    public string MetaLine
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Book.Publisher))
            {
                parts.Add(Book.Publisher);
            }

            if (Book.PublishedYear.HasValue)
            {
                parts.Add(Book.PublishedYear.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(Book.Language))
            {
                parts.Add(Book.Language!.ToUpperInvariant());
            }

            return parts.Count == 0 ? "Catalog result" : string.Join(" | ", parts);
        }
    }

    public string Summary => string.IsNullOrWhiteSpace(Book.Description)
        ? "Open the details pane to inspect formats, source information, and available actions."
        : Book.Description!;

    public string SourceBadge => Book.Source.SourceName;
    public string FormatBadge => Book.Formats.Count == 0 ? "metadata" : Book.Formats[0].ToUpperInvariant();
}
