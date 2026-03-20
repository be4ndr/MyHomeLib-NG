using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers;

internal static class NormalizedBookDeduplicator
{
    public static IReadOnlyList<NormalizedBook> Deduplicate(IEnumerable<NormalizedBook> books)
    {
        return books
            .GroupBy(book => $"{book.Source}|{book.SourceId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }
}
