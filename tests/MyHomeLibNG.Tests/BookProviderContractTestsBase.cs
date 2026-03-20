using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using Xunit;

namespace MyHomeLibNG.Tests;

public abstract class BookProviderContractTestsBase
{
    protected abstract IBookProvider CreateProvider();
    protected abstract string SearchQuery { get; }

    [Fact]
    public async Task SearchAsync_ReturnsNormalizedBooks()
    {
        var provider = CreateProvider();
        var results = await provider.SearchAsync(SearchQuery);

        Assert.NotEmpty(results);
        Assert.All(results, AssertRequiredFields);
        Assert.All(results, book => Assert.All(book.Authors, author => Assert.False(string.IsNullOrWhiteSpace(author))));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSameLogicalBook_WhenSupported()
    {
        var provider = CreateProvider();
        if (!provider.Capabilities.SupportsDetails)
        {
            Assert.False(provider.Capabilities.SupportsDetails);
            return;
        }

        var searchResult = (await provider.SearchAsync(SearchQuery)).First();
        var loaded = await provider.GetByIdAsync(searchResult.SourceId);

        Assert.NotNull(loaded);
        Assert.Equal(searchResult.Source, loaded.Source);
        Assert.Equal(searchResult.SourceId, loaded.SourceId);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Title));
    }

    [Fact]
    public async Task OpenContentAsync_ReturnsReadableStream_WhenSupported()
    {
        var provider = CreateProvider();
        if (!provider.Capabilities.SupportsContentStream)
        {
            Assert.False(provider.Capabilities.SupportsContentStream);
            return;
        }

        var searchResult = (await provider.SearchAsync(SearchQuery)).First();
        await using var stream = await provider.OpenContentAsync(searchResult.SourceId);
        Assert.True(stream.CanRead);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.True(buffer.Length > 0);
    }

    private static void AssertRequiredFields(NormalizedBook book)
    {
        Assert.False(string.IsNullOrWhiteSpace(book.Title));
        Assert.False(string.IsNullOrWhiteSpace(book.Source));
        Assert.False(string.IsNullOrWhiteSpace(book.SourceId));
    }
}
