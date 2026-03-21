using MyHomeLibNG.Core.Enums;

namespace MyHomeLibNG.Core.Models;

public sealed class LibraryStructure
{
    public long LibraryProfileId { get; init; }
    public LibraryType LibraryType { get; init; }
    public IReadOnlyList<LibrarySourceLocation> Sources { get; init; } = Array.Empty<LibrarySourceLocation>();
}
