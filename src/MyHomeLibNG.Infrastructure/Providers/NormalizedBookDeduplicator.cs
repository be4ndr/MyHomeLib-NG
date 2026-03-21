using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers;

internal static class NormalizedBookDeduplicator
{
    public static IReadOnlyList<NormalizedBook> Deduplicate(IEnumerable<NormalizedBook> books)
    {
        return BookDeduplication.CollapseExactDuplicates(books);
    }
}
