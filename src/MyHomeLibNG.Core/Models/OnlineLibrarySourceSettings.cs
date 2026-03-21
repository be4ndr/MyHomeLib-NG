namespace MyHomeLibNG.Core.Models;

public sealed class OnlineLibrarySourceSettings
{
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string? SearchEndpoint { get; init; }
}
