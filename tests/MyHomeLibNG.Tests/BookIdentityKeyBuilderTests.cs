using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class BookIdentityKeyBuilderTests
{
    [Fact]
    public void BuildLogicalIdentity_NormalizesTitleAuthorsAndIdentifiers()
    {
        var left = new NormalizedBook
        {
            Title = " The Time-Machine ",
            Authors = ["H. G. Wells", "Another Author"],
            PublishedYear = 1895,
            Isbn13 = "978-1-234-56789-7"
        };
        var right = new NormalizedBook
        {
            Title = "the time machine",
            Authors = ["Another Author", "H G Wells"],
            PublishedYear = 1895,
            Isbn13 = "9781234567897"
        };

        var leftIdentity = BookIdentityKeyBuilder.BuildLogicalIdentity(left);
        var rightIdentity = BookIdentityKeyBuilder.BuildLogicalIdentity(right);

        Assert.Equal(leftIdentity, rightIdentity);
    }

    [Fact]
    public void CollapseExactDuplicates_RemovesOnlyProviderDuplicates()
    {
        var books = new[]
        {
            new NormalizedBook
            {
                Title = "Book One",
                Source = "Offline",
                SourceId = "1",
                Authors = ["Ana Author"]
            },
            new NormalizedBook
            {
                Title = "Book One Duplicate",
                Source = "Offline",
                SourceId = "1",
                Authors = ["Ana Author"]
            },
            new NormalizedBook
            {
                Title = "Book One",
                Source = "Open Library",
                SourceId = "OL1",
                Authors = ["Ana Author"]
            }
        };

        var deduplicated = BookDeduplication.CollapseExactDuplicates(books);
        var grouped = BookDeduplication.GroupByLogicalIdentity(deduplicated);

        Assert.Equal(2, deduplicated.Count);
        Assert.Single(grouped);
        Assert.Equal(2, grouped.Single().Value.Count);
    }
}
