namespace MyHomeLibNG.Core.Models;

public sealed class BookSourceDescriptor
{
    public long LibraryProfileId { get; init; }
    public string ProviderId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
}
