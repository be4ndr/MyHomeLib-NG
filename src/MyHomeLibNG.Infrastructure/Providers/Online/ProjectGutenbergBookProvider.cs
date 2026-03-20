using System.Xml.Linq;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class ProjectGutenbergBookProvider : IBookProvider
{
    private readonly HttpClient _httpClient;
    private readonly TransientHttpExecutor _httpExecutor;

    public ProjectGutenbergBookProvider(HttpClient httpClient, TransientHttpExecutor httpExecutor)
    {
        _httpClient = httpClient;
        _httpExecutor = httpExecutor;
    }

    public string Id => BookProviderIds.ProjectGutenberg;
    public string DisplayName => "Project Gutenberg";
    public BookProviderCapabilities Capabilities { get; } = new()
    {
        SupportsSearch = true,
        SupportsDetails = false,
        SupportsContentStream = false
    };

    public async Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var response = await _httpExecutor.ExecuteAsync(
            token => _httpClient.GetAsync($"/ebooks/search.opds/?query={Uri.EscapeDataString(query)}", token),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return NormalizedBookDeduplicator.Deduplicate(ParseFeed(payload));
    }

    public Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<NormalizedBook?>(null);

    public Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Project Gutenberg content streaming is not enabled in this provider.");

    private static IReadOnlyList<NormalizedBook> ParseFeed(string xml)
    {
        var document = XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";

        return document
            .Descendants(atom + "entry")
            .Select(entry =>
            {
                var entryId = entry.Element(atom + "id")?.Value ?? string.Empty;
                var sourceId = entryId.Split('/').LastOrDefault() ?? entryId;
                var downloadLinks = entry.Elements(atom + "link")
                    .Where(link => string.Equals(link.Attribute("rel")?.Value, "http://opds-spec.org/acquisition", StringComparison.OrdinalIgnoreCase))
                    .Select(link => new BookDownloadLink
                    {
                        Label = link.Attribute("type")?.Value ?? "download",
                        Url = link.Attribute("href")?.Value ?? string.Empty,
                        Format = link.Attribute("type")?.Value
                    })
                    .Where(link => !string.IsNullOrWhiteSpace(link.Url))
                    .ToArray();

                return new NormalizedBook
                {
                    Source = "Project Gutenberg",
                    SourceId = sourceId,
                    Title = entry.Element(atom + "title")?.Value ?? sourceId,
                    Authors = entry.Elements(atom + "author")
                        .Select(author => author.Element(atom + "name")?.Value)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Cast<string>()
                        .ToArray(),
                    Subjects = entry.Elements(atom + "category")
                        .Select(category => category.Attribute("term")?.Value)
                        .Where(term => !string.IsNullOrWhiteSpace(term))
                        .Cast<string>()
                        .ToArray(),
                    DownloadLinks = downloadLinks,
                    Formats = downloadLinks
                        .Select(link => link.Format)
                        .Where(format => !string.IsNullOrWhiteSpace(format))
                        .Cast<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    ReadLink = entry.Elements(atom + "link")
                        .FirstOrDefault(link => string.Equals(link.Attribute("type")?.Value, "text/html", StringComparison.OrdinalIgnoreCase))
                        ?.Attribute("href")?.Value
                };
            })
            .Where(book => !string.IsNullOrWhiteSpace(book.Title) && !string.IsNullOrWhiteSpace(book.SourceId))
            .ToArray();
    }
}
