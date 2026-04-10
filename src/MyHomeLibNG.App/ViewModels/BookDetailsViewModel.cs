using Avalonia.Media.Imaging;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.App.ViewModels;

public sealed class BookDetailsViewModel
{
    public BookDetailsViewModel(BookDetails details)
    {
        Details = details;
        CoverImage = CreateBitmap(details.CoverThumbnail);
    }

    public BookDetails Details { get; }
    public Bitmap? CoverImage { get; }
    public string Title => Details.Title;
    public string AuthorsLine => Details.Authors.Count == 0 ? "Unknown author" : string.Join(", ", Details.Authors);
    public string SeriesLine => string.IsNullOrWhiteSpace(Details.Series) ? "Standalone" : Details.Series!;
    public string Description => string.IsNullOrWhiteSpace(Details.Description)
        ? "No extended description is available for this title yet."
        : Details.Description!;
    public string ProviderName => Details.Source.SourceName;
    public string ProviderId => Details.Source.ProviderId;
    public string SourceId => Details.Source.SourceId;
    public string FormatsLine => Details.Formats.Count == 0 ? "Not specified" : string.Join(", ", Details.Formats.Select(format => format.ToUpperInvariant()));
    public string SubjectsLine => Details.Subjects.Count == 0 ? "Not specified" : string.Join(", ", Details.Subjects);
    public string PublisherLine => string.IsNullOrWhiteSpace(Details.Publisher) ? "Unknown" : Details.Publisher!;
    public string YearLine => Details.PublishedYear?.ToString() ?? "Unknown";
    public string LanguageLine => string.IsNullOrWhiteSpace(Details.Language) ? "Unknown" : Details.Language!.ToUpperInvariant();
    public string IsbnLine => Details.Isbn13 ?? Details.Isbn10 ?? "Not available";
    public bool HasCoverImage => CoverImage is not null;
    public bool HasContentHandle => Details.ContentHandle is not null;
    public bool HasReadLink => !string.IsNullOrWhiteSpace(Details.ReadLink);
    public bool HasBorrowLink => !string.IsNullOrWhiteSpace(Details.BorrowLink);
    public bool HasDownloadLink => Details.DownloadLinks.Count > 0;
    public bool CanPerformPrimaryAction => HasContentHandle || HasReadLink || HasDownloadLink;
    public bool CanCopyLink => HasReadLink || HasBorrowLink || HasDownloadLink;

    public string PrimaryActionLabel
    {
        get
        {
            if (HasContentHandle)
            {
                return "Open";
            }

            if (HasReadLink)
            {
                return "Read online";
            }

            if (HasDownloadLink)
            {
                return "Download";
            }

            return "Unavailable";
        }
    }

    public string SecondaryActionLabel => CanCopyLink ? "Copy link" : "No link";

    public string? PreferredLink => Details.ReadLink
        ?? Details.BorrowLink
        ?? Details.DownloadLinks.FirstOrDefault()?.Url;

    private static Bitmap? CreateBitmap(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            return new Bitmap(new MemoryStream(bytes, writable: false));
        }
        catch
        {
            return null;
        }
    }
}
