using System.Text.Json;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class GoogleBooksBookProvider : IBookProvider
{
    private readonly HttpClient _httpClient;
    private readonly TransientHttpExecutor _httpExecutor;

    public GoogleBooksBookProvider(HttpClient httpClient, TransientHttpExecutor httpExecutor)
    {
        _httpClient = httpClient;
        _httpExecutor = httpExecutor;
    }

    public string Id => BookProviderIds.GoogleBooks;
    public string DisplayName => "Google Books";
    public BookProviderCapabilities Capabilities { get; } = new()
    {
        SupportsSearch = true,
        SupportsDetails = true,
        SupportsContentStream = false
    };

    public async Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var response = await _httpExecutor.ExecuteAsync(
            token => _httpClient.GetAsync($"/books/v1/volumes?q={Uri.EscapeDataString(query)}", token),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("items", out var items))
        {
            return Array.Empty<NormalizedBook>();
        }

        return NormalizedBookDeduplicator.Deduplicate(items.EnumerateArray().Select(MapBook));
    }

    public async Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpExecutor.ExecuteAsync(
            token => _httpClient.GetAsync($"/books/v1/volumes/{sourceId}", token),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return MapBook(document.RootElement);
    }

    public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Google Books does not expose content streams through this provider.");

    private static NormalizedBook MapBook(JsonElement element)
    {
        var volumeInfo = element.GetProperty("volumeInfo");
        var identifiers = volumeInfo.TryGetProperty("industryIdentifiers", out var industryIdentifiers)
            ? industryIdentifiers.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();

        return new NormalizedBook
        {
            Source = "Google Books",
            SourceId = element.GetProperty("id").GetString() ?? string.Empty,
            Title = volumeInfo.GetProperty("title").GetString() ?? string.Empty,
            Authors = volumeInfo.TryGetProperty("authors", out var authors)
                ? authors.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
                : Array.Empty<string>(),
            Language = volumeInfo.TryGetProperty("language", out var language) ? language.GetString() : null,
            Description = volumeInfo.TryGetProperty("description", out var description) ? description.GetString() : null,
            Subjects = volumeInfo.TryGetProperty("categories", out var categories)
                ? categories.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
                : Array.Empty<string>(),
            Publisher = volumeInfo.TryGetProperty("publisher", out var publisher) ? publisher.GetString() : null,
            PublishedYear = TryParseYear(volumeInfo.TryGetProperty("publishedDate", out var publishedDate) ? publishedDate.GetString() : null),
            Isbn10 = identifiers.FirstOrDefault(identifier => identifier.GetProperty("type").GetString() == "ISBN_10").GetPropertyOrDefault("identifier"),
            Isbn13 = identifiers.FirstOrDefault(identifier => identifier.GetProperty("type").GetString() == "ISBN_13").GetPropertyOrDefault("identifier"),
            CoverUrl = volumeInfo.TryGetProperty("imageLinks", out var imageLinks) && imageLinks.TryGetProperty("thumbnail", out var thumbnail)
                ? thumbnail.GetString()
                : null,
            ReadLink = element.TryGetProperty("accessInfo", out var accessInfo) && accessInfo.TryGetProperty("webReaderLink", out var readerLink)
                ? readerLink.GetString()
                : null,
            BorrowLink = element.TryGetProperty("saleInfo", out var saleInfo) && saleInfo.TryGetProperty("buyLink", out var buyLink)
                ? buyLink.GetString()
                : null
        };
    }

    private static int? TryParseYear(string? publishedDate)
    {
        if (string.IsNullOrWhiteSpace(publishedDate) || publishedDate.Length < 4)
        {
            return null;
        }

        return int.TryParse(publishedDate[..4], out var year) ? year : null;
    }
}
