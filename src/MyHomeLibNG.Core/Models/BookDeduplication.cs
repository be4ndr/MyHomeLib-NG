namespace MyHomeLibNG.Core.Models;

public static class BookDeduplication
{
    public static IReadOnlyList<NormalizedBook> CollapseExactDuplicates(IEnumerable<NormalizedBook> books)
    {
        return books
            .GroupBy(BookIdentityKeyBuilder.BuildExactProviderKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public static IReadOnlyList<BookSearchResult> CollapseExactDuplicates(IEnumerable<BookSearchResult> books)
    {
        return books
            .GroupBy(BookIdentityKeyBuilder.BuildExactProviderKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    public static IReadOnlyDictionary<BookLogicalIdentity, IReadOnlyList<NormalizedBook>> GroupByLogicalIdentity(
        IEnumerable<NormalizedBook> books)
    {
        return books
            .GroupBy(BookIdentityKeyBuilder.BuildLogicalIdentity)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NormalizedBook>)group.ToArray());
    }
}
