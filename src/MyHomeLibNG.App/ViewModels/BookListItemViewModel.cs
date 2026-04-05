using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class BookListItemViewModel
{
    public BookListItemViewModel(BookSearchResult book, bool activeLibraryHasSourceIssues = false)
    {
        Book = book;
        ActiveLibraryHasSourceIssues = activeLibraryHasSourceIssues;
    }

    public BookSearchResult Book { get; }
    private bool ActiveLibraryHasSourceIssues { get; }
    public string Title => Book.Title;
    public string AuthorsLine => Book.Authors.Count == 0 ? "Unknown author" : string.Join(", ", Book.Authors);
    public string? SeriesLine => null;
    public bool HasSeries => !string.IsNullOrWhiteSpace(SeriesLine);
    public string GenreLine => Book.Subjects.Count == 0 ? "General catalog" : string.Join(", ", Book.Subjects.Take(2));
    public bool HasGenre => Book.Subjects.Count > 0;
    public string YearLine => Book.PublishedYear?.ToString() ?? "Year unknown";
    public string LanguageBadge => string.IsNullOrWhiteSpace(Book.Language) ? "LANG?" : Book.Language!.ToUpperInvariant();
    public bool HasLocalContent => Book.ContentHandle is not null;
    public bool HasBrokenLocalContent => HasLocalContent && ActiveLibraryHasSourceIssues;
    public bool ShowLocalReadyState => HasLocalContent && !HasBrokenLocalContent;
    public bool ShowLocalUnavailableState => !HasLocalContent;
    public bool ShowLocalWarningState => HasBrokenLocalContent;
    public string LocalAvailabilityLabel => HasBrokenLocalContent
        ? "Local path issue"
        : HasLocalContent
            ? "Local file"
            : "Remote only";

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
