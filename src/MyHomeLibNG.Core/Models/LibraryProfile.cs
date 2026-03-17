using MyHomeLibNext.Core.Enums;

namespace MyHomeLibNext.Core.Models;

public sealed class LibraryProfile
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public LibraryType LibraryType { get; init; }
    public string ConnectionInfo { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastOpenedAtUtc { get; init; }
}
