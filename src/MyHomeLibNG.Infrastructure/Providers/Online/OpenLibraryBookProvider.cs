using System.Text.Json;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class OpenLibraryBookProvider : IBookProvider
{
    private readonly HttpClient _httpClient;
    private readonly TransientHttpExecutor _httpExecutor;

    public OpenLibraryBookProvider(HttpClient httpClient, TransientHttpExecutor httpExecutor)
    {
        _httpClient = httpClient;
        _httpExecutor = httpExecutor;
    }

    public string Id => BookProviderIds.OpenLibrary;
    public string DisplayName => "Open Library";
    public BookProviderCapabilities Capabilities { get; } = new()
    {
        SupportsSearch = true,
        SupportsDetails = true,
        SupportsContentStream = false
    };

    public async Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var response = await _httpExecutor.ExecuteAsync(
            token => _httpClient.GetAsync($"/search.json?q={Uri.EscapeDataString(query)}", token),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var docs = document.RootElement.GetProperty("docs");

        return NormalizedBookDeduplicator.Deduplicate(docs.EnumerateArray().Select(MapSearchBook));
    }

    public async Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpExecutor.ExecuteAsync(
            token => _httpClient.GetAsync($"/works/{sourceId}.json", token),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return MapDetailsBook(document.RootElement, sourceId);
    }

    public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Open Library does not expose content streams through this provider.");

    private static NormalizedBook MapSearchBook(JsonElement element)
    {
        var key = element.GetProperty("key").GetString() ?? string.Empty;
        var sourceId = key.Split('/').LastOrDefault() ?? key;
        var authors = element.TryGetProperty("author_name", out var authorNames)
            ? authorNames.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
            : Array.Empty<string>();
        var subjects = element.TryGetProperty("subject", out var subjectValues)
            ? subjectValues.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
            : Array.Empty<string>();
        var isbns = element.TryGetProperty("isbn", out var isbnValues)
            ? isbnValues.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
            : Array.Empty<string>();

        return new NormalizedBook
        {
            Source = "Open Library",
            SourceId = sourceId,
            Title = element.GetProperty("title").GetString() ?? sourceId,
            Authors = authors,
            Language = element.TryGetProperty("language", out var languages) && languages.GetArrayLength() > 0
                ? languages[0].GetString()
                : null,
            Subjects = subjects,
            Publisher = element.TryGetProperty("publisher", out var publishers) && publishers.GetArrayLength() > 0
                ? publishers[0].GetString()
                : null,
            PublishedYear = element.TryGetProperty("first_publish_year", out var yearElement) && yearElement.TryGetInt32(out var year)
                ? year
                : null,
            Isbn10 = isbns.FirstOrDefault(isbn => isbn.Length == 10),
            Isbn13 = isbns.FirstOrDefault(isbn => isbn.Length == 13),
            CoverUrl = element.TryGetProperty("cover_i", out var coverId)
                ? $"https://covers.openlibrary.org/b/id/{coverId.GetInt32()}-L.jpg"
                : null,
            ReadLink = $"https://openlibrary.org{key}"
        };
    }

    private static NormalizedBook MapDetailsBook(JsonElement element, string sourceId)
    {
        var description = element.TryGetProperty("description", out var descriptionValue)
            ? descriptionValue.ValueKind == JsonValueKind.Object && descriptionValue.TryGetProperty("value", out var nestedDescription)
                ? nestedDescription.GetString()
                : descriptionValue.GetString()
            : null;
        var subjects = element.TryGetProperty("subjects", out var subjectValues)
            ? subjectValues.EnumerateArray().Select(item => item.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray()
            : Array.Empty<string>();

        return new NormalizedBook
        {
            Source = "Open Library",
            SourceId = sourceId,
            Title = element.GetProperty("title").GetString() ?? sourceId,
            Description = description,
            Subjects = subjects,
            ReadLink = $"https://openlibrary.org/works/{sourceId}"
        };
    }
}
