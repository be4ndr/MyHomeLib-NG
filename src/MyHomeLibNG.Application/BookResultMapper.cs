using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

internal static class BookResultMapper
{
    public static BookSearchResult ToSearchResult(
        LibraryProfile profile,
        BookProviderCapabilities capabilities,
        NormalizedBook book)
    {
        return new BookSearchResult
        {
            Title = book.Title,
            Source = CreateSourceDescriptor(profile, book),
            Authors = book.Authors,
            Series = book.Series,
            Language = book.Language,
            Description = book.Description,
            Subjects = book.Subjects,
            Publisher = book.Publisher,
            PublishedYear = book.PublishedYear,
            Isbn10 = book.Isbn10,
            Isbn13 = book.Isbn13,
            CoverUrl = book.CoverUrl,
            Formats = book.Formats,
            DownloadLinks = book.DownloadLinks,
            ReadLink = book.ReadLink,
            BorrowLink = book.BorrowLink,
            ContentHandle = CreateContentHandle(profile, capabilities, book)
        };
    }

    public static BookDetails ToDetails(
        LibraryProfile profile,
        BookProviderCapabilities capabilities,
        NormalizedBook book)
    {
        return new BookDetails
        {
            Title = book.Title,
            Source = CreateSourceDescriptor(profile, book),
            Authors = book.Authors,
            Series = book.Series,
            Language = book.Language,
            Description = book.Description,
            Subjects = book.Subjects,
            Publisher = book.Publisher,
            PublishedYear = book.PublishedYear,
            Isbn10 = book.Isbn10,
            Isbn13 = book.Isbn13,
            CoverUrl = book.CoverUrl,
            Formats = book.Formats,
            DownloadLinks = book.DownloadLinks,
            ReadLink = book.ReadLink,
            BorrowLink = book.BorrowLink,
            ContentHandle = CreateContentHandle(profile, capabilities, book)
        };
    }

    private static BookSourceDescriptor CreateSourceDescriptor(LibraryProfile profile, NormalizedBook book)
    {
        return new BookSourceDescriptor
        {
            LibraryProfileId = profile.Id,
            ProviderId = profile.ProviderId,
            SourceName = book.Source,
            SourceId = book.SourceId
        };
    }

    private static BookContentHandle? CreateContentHandle(
        LibraryProfile profile,
        BookProviderCapabilities capabilities,
        NormalizedBook book)
    {
        if (!capabilities.SupportsContentStream)
        {
            return null;
        }

        return new BookContentHandle
        {
            LibraryProfileId = profile.Id,
            ProviderId = profile.ProviderId,
            SourceId = book.SourceId
        };
    }
}
