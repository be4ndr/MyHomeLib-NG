namespace MyHomeLibNG.Core.Models;

public sealed class BookProviderCapabilities
{
    public bool SupportsSearch { get; init; }
    public bool SupportsDetails { get; init; }
    public bool SupportsContentStream { get; init; }
}
