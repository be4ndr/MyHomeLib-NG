using System.Text;

namespace MyHomeLibNG.Infrastructure.Repositories;

internal static class BookSearchTextNormalizer
{
    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static string BuildSearchText(
        string? title,
        string? authors,
        string? series,
        string? genres,
        string? annotation,
        string? language,
        string? fileName)
    {
        var parts = new[]
        {
            NormalizeForSearch(title),
            NormalizeForSearch(authors),
            NormalizeForSearch(series),
            NormalizeForSearch(genres),
            NormalizeForSearch(annotation),
            NormalizeForSearch(language),
            NormalizeForSearch(fileName)
        };

        return string.Join(' ', parts.Where(part => part.Length > 0));
    }
}
