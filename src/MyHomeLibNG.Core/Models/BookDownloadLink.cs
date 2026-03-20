namespace MyHomeLibNG.Core.Models;

public sealed class BookDownloadLink
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Format { get; init; }
}
