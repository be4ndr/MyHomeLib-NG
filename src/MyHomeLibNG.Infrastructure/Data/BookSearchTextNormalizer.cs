using System.Globalization;
using System.Text;

namespace MyHomeLibNG.Infrastructure.Data;

/// <summary>
/// Builds and normalizes Unicode text for case-insensitive search persistence.
/// </summary>
internal static class BookSearchTextNormalizer
{
    /// <summary>
    /// Normalizes a single text value for search.
    /// </summary>
    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .ToLower(CultureInfo.InvariantCulture);

        return CollapseWhitespace(normalized);
    }

    /// <summary>
    /// Builds a compact normalized search text from book metadata fields.
    /// </summary>
    public static string BuildSearchText(
        string? title,
        string? authors,
        string? series,
        string? genres,
        string? annotation,
        string? language,
        string? fileName)
    {
        var builder = new StringBuilder();
        AppendNormalized(builder, title);
        AppendNormalized(builder, authors);
        AppendNormalized(builder, series);
        AppendNormalized(builder, genres);
        AppendNormalized(builder, annotation);
        AppendNormalized(builder, language);
        AppendNormalized(builder, fileName);
        return builder.ToString();
    }

    private static void AppendNormalized(StringBuilder builder, string? value)
    {
        var normalized = NormalizeForSearch(value);
        if (normalized.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(normalized);
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inWhitespace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0)
                {
                    inWhitespace = true;
                }

                continue;
            }

            if (inWhitespace)
            {
                builder.Append(' ');
                inWhitespace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
