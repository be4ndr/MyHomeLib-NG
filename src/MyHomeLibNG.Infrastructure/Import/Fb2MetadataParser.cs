using System.Globalization;
using System.Xml.Linq;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using SkiaSharp;

namespace MyHomeLibNG.Infrastructure.Import;

public sealed class Fb2MetadataParser : IFb2MetadataParser
{
    public Task<Fb2BookMetadata> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stream);

        var document = XDocument.Load(stream, LoadOptions.None);
        var description = FindChild(document.Root, "description");
        var titleInfo = FindChild(description, "title-info");
        var publishInfo = FindChild(description, "publish-info");
        var coverReference = ResolveCoverReference(titleInfo);
        var coverImageBytes = ResolveCoverImageBytes(document, coverReference);

        return Task.FromResult(new Fb2BookMetadata
        {
            Title = ExtractTitle(titleInfo),
            Authors = ExtractAuthors(titleInfo),
            Genres = ExtractGenres(titleInfo),
            Annotation = ExtractAnnotation(titleInfo),
            Series = ExtractSeries(titleInfo),
            SeriesNumber = ExtractSeriesNumber(titleInfo),
            Language = NullIfWhiteSpace(FindChildValue(titleInfo, "lang")),
            PublishYear = ExtractPublishYear(titleInfo, publishInfo),
            CoverReference = coverReference,
            CoverImageBytes = coverImageBytes,
            ThumbnailBytes = CreateThumbnailBytes(coverImageBytes)
        });
    }

    private static string ExtractTitle(XElement? titleInfo)
    {
        var bookTitle = NullIfWhiteSpace(FindChildValue(titleInfo, "book-title"));
        return bookTitle ?? string.Empty;
    }

    private static IReadOnlyList<string> ExtractAuthors(XElement? titleInfo)
    {
        if (titleInfo is null)
        {
            return Array.Empty<string>();
        }

        return titleInfo
            .Elements()
            .Where(element => element.Name.LocalName == "author")
            .Select(FormatAuthor)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? FormatAuthor(XElement authorElement)
    {
        var parts = new[]
            {
                FindChildValue(authorElement, "first-name"),
                FindChildValue(authorElement, "middle-name"),
                FindChildValue(authorElement, "last-name")
            }
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        if (parts.Length > 0)
        {
            return string.Join(" ", parts);
        }

        return NullIfWhiteSpace(FindChildValue(authorElement, "nickname"));
    }

    private static IReadOnlyList<string> ExtractGenres(XElement? titleInfo)
    {
        if (titleInfo is null)
        {
            return Array.Empty<string>();
        }

        return titleInfo
            .Elements()
            .Where(element => element.Name.LocalName == "genre")
            .Select(element => NullIfWhiteSpace(element.Value))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ExtractAnnotation(XElement? titleInfo)
    {
        var annotation = FindChild(titleInfo, "annotation");
        if (annotation is null)
        {
            return null;
        }

        var blocks = annotation
            .DescendantsAndSelf()
            .Where(element => element.Name.LocalName is "p" or "subtitle" or "title")
            .Select(element => NormalizeWhitespace(element.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (blocks.Length > 0)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, blocks);
        }

        return NullIfWhiteSpace(NormalizeWhitespace(annotation.Value));
    }

    private static string? ExtractSeries(XElement? titleInfo)
        => NullIfWhiteSpace(titleInfo?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "sequence")?
            .Attribute("name")?
            .Value);

    private static int? ExtractSeriesNumber(XElement? titleInfo)
        => TryParseInteger(titleInfo?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "sequence")?
            .Attribute("number")?
            .Value);

    private static int? ExtractPublishYear(XElement? titleInfo, XElement? publishInfo)
    {
        var publishYear = TryParseYear(FindChildValue(publishInfo, "year"));
        if (publishYear.HasValue)
        {
            return publishYear;
        }

        var dateElement = FindChild(titleInfo, "date");
        return TryParseYear(dateElement?.Attribute("value")?.Value)
               ?? TryParseYear(dateElement?.Value);
    }

    private static string? ResolveCoverReference(XElement? titleInfo)
    {
        var image = FindChild(FindChild(titleInfo, "coverpage"), "image");
        var hrefValue = image?
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "href")
            ?.Value;

        return NullIfWhiteSpace(hrefValue);
    }

    private static byte[]? ResolveCoverImageBytes(XDocument document, string? coverReference)
    {
        var binaryId = NormalizeBinaryId(coverReference);
        if (binaryId is null)
        {
            return null;
        }

        var binary = document
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "binary" &&
                string.Equals(
                    NormalizeBinaryId(element.Attribute("id")?.Value),
                    binaryId,
                    StringComparison.OrdinalIgnoreCase));

        if (binary is null)
        {
            return null;
        }

        try
        {
            var payload = new string(binary.Value.Where(character => !char.IsWhiteSpace(character)).ToArray());
            return payload.Length == 0 ? null : Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? CreateThumbnailBytes(byte[]? coverImageBytes)
    {
        if (coverImageBytes is null || coverImageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var sourceBitmap = SKBitmap.Decode(coverImageBytes);
            if (sourceBitmap is null || sourceBitmap.Width <= 0 || sourceBitmap.Height <= 0)
            {
                return null;
            }

            const int maxWidth = 160;
            const int maxHeight = 240;
            var scale = Math.Min((float)maxWidth / sourceBitmap.Width, (float)maxHeight / sourceBitmap.Height);
            if (scale > 1f)
            {
                scale = 1f;
            }

            var width = Math.Max(1, (int)Math.Round(sourceBitmap.Width * scale));
            var height = Math.Max(1, (int)Math.Round(sourceBitmap.Height * scale));
            var imageInfo = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(imageInfo);
            if (surface is null)
            {
                return null;
            }

            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawBitmap(sourceBitmap, new SKRect(0, 0, width, height));
            surface.Canvas.Flush();

            using var image = surface.Snapshot();
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            return encoded?.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static XElement? FindChild(XElement? parent, string localName)
        => parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string? FindChildValue(XElement? parent, string localName)
        => FindChild(parent, localName)?.Value;

    private static string? NormalizeBinaryId(string? value)
    {
        var normalized = NullIfWhiteSpace(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized.TrimStart('#');
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static int? TryParseInteger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exactValue))
        {
            return exactValue;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : null;
    }

    private static int? TryParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
        {
            return null;
        }

        for (var index = 0; index <= digits.Length - 4; index++)
        {
            if (int.TryParse(digits.Substring(index, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            {
                return year;
            }
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
