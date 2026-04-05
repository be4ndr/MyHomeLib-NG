namespace MyHomeLibNG.App.ViewModels;

internal static class SearchTextNormalizer
{
    public static bool IsMatchAllToken(string? value)
        => string.Equals(value?.Trim(), "*", StringComparison.Ordinal);

    public static bool IsDigitBucketToken(string? value)
        => string.Equals(value?.Trim(), "#", StringComparison.Ordinal);

    public static string NormalizeForSearch(string? value, bool ignoreLeadingArticles = false)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return ignoreLeadingArticles
            ? StripLeadingEnglishArticles(trimmed)
            : trimmed;
    }

    public static bool MatchesValue(
        string? candidate,
        string? filter,
        bool exactMatch,
        bool ignoreLeadingArticles = false)
    {
        if (string.IsNullOrWhiteSpace(filter) || IsMatchAllToken(filter))
        {
            return true;
        }

        if (IsDigitBucketToken(filter))
        {
            return StartsWithDigit(candidate, ignoreLeadingArticles);
        }

        var normalizedCandidate = NormalizeForSearch(candidate, ignoreLeadingArticles);
        var normalizedFilter = NormalizeForSearch(filter, ignoreLeadingArticles);
        if (normalizedCandidate.Length == 0 || normalizedFilter.Length == 0)
        {
            return false;
        }

        return exactMatch
            ? string.Equals(normalizedCandidate, normalizedFilter, StringComparison.OrdinalIgnoreCase)
            : normalizedCandidate.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }

    public static bool StartsWithDigit(string? candidate, bool ignoreLeadingArticles = false)
    {
        var normalized = NormalizeForSearch(candidate, ignoreLeadingArticles);
        return normalized.Length > 0 && char.IsDigit(normalized[0]);
    }

    public static string GetAlphabetBucket(string? candidate, bool ignoreLeadingArticles = false)
    {
        var normalized = NormalizeForSearch(candidate, ignoreLeadingArticles);
        if (normalized.Length == 0)
        {
            return "*";
        }

        var first = char.ToUpperInvariant(normalized[0]);
        if (char.IsDigit(first))
        {
            return "#";
        }

        return char.IsLetter(first) ? first.ToString() : "*";
    }

    private static string StripLeadingEnglishArticles(string value)
    {
        if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return value[4..].TrimStart();
        }

        if (value.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return value[2..].TrimStart();
        }

        return value;
    }
}
