namespace MyHomeLibNG.Core.Models;

public sealed class Fb2ParsingOptions
{
    public static Fb2ParsingOptions Full { get; } = new();

    public static Fb2ParsingOptions FastImport { get; } = new()
    {
        ExtractAnnotation = false,
        ExtractCoverImages = false,
        ExtractThumbnail = false
    };

    public bool ExtractAnnotation { get; init; } = true;
    public bool ExtractCoverImages { get; init; } = true;
    public bool ExtractThumbnail { get; init; } = true;
}
