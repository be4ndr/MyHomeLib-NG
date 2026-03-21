using System.Text;

namespace MyHomeLibNG.Core.Models;

public static class BookIdentityKeyBuilder
{
    public static string BuildExactProviderKey(NormalizedBook book)
        => BuildExactProviderKey(book.Source, book.SourceId);

    public static string BuildExactProviderKey(BookSearchResult book)
        => BuildExactProviderKey(book.Source.SourceName, book.Source.SourceId);

    public static string BuildExactProviderKey(string sourceName, string sourceId)
        => $"{sourceName}|{sourceId}";

    public static BookLogicalIdentity BuildLogicalIdentity(NormalizedBook book)
        => BuildLogicalIdentity(book.Title, book.Authors, book.PublishedYear, book.Isbn10, book.Isbn13);

    public static BookLogicalIdentity BuildLogicalIdentity(BookSearchResult book)
        => BuildLogicalIdentity(book.Title, book.Authors, book.PublishedYear, book.Isbn10, book.Isbn13);

    public static BookLogicalIdentity BuildLogicalIdentity(
        string title,
        IReadOnlyList<string> authors,
        int? publishedYear,
        string? isbn10,
        string? isbn13,
        string? series = null)
    {
        var normalizedAuthors = authors
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Select(NormalizeText)
            .Where(author => author.Length > 0)
            .OrderBy(author => author, StringComparer.Ordinal)
            .ToArray();

        return new BookLogicalIdentity(
            NormalizeText(title),
            string.Join('|', normalizedAuthors),
            publishedYear,
            NormalizeIdentifier(isbn10),
            NormalizeIdentifier(isbn13),
            NormalizeTextOrNull(series));
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append(' ');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim();
    }

    private static string? NormalizeTextOrNull(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var filtered = value
            .Where(character => char.IsLetterOrDigit(character))
            .Select(char.ToLowerInvariant)
            .ToArray();

        return filtered.Length == 0 ? null : new string(filtered);
    }
}
