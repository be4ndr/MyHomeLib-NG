using MyHomeLibNG.Core.Enums;

namespace MyHomeLibNG.Core.Models;

public sealed class LibrarySourceLocation
{
    public SourceKind Kind { get; init; }
    public string PathOrUri { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public long? SizeBytes { get; init; }
}
