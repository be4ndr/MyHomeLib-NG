using MyHomeLibNext.Core.Enums;

namespace MyHomeLibNext.Core.Models;

public sealed class BookSource
{
    public long Id { get; init; }
    public SourceKind Kind { get; init; }
    public string PathOrUri { get; init; } = string.Empty;
    public FileFormat FileFormat { get; init; }
    public long SizeBytes { get; init; }
}
