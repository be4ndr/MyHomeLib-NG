using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using SkiaSharp;

namespace MyHomeLibNG.Infrastructure.Import;

public sealed class Fb2MetadataParser : IFb2MetadataParser
{
    static Fb2MetadataParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<Fb2BookMetadata> ParseAsync(
        Stream stream,
        Fb2ParsingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(stream);

        options ??= Fb2ParsingOptions.Full;
        var metadata = new MutableMetadata();
        var settings = new XmlReaderSettings
        {
            Async = false,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = false
        };

        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "title-info":
                    ParseTitleInfo(reader, metadata, options, cancellationToken);
                    break;
                case "publish-info":
                    ParsePublishInfo(reader, metadata, cancellationToken);
                    break;
                case "binary" when options.ExtractCoverImages:
                    TryReadCoverBinary(reader, metadata);
                    break;
            }
        }

        var coverBytes = metadata.CoverImageBytes;
        return Task.FromResult(new Fb2BookMetadata
        {
            Title = metadata.Title ?? string.Empty,
            Authors = metadata.Authors.ToArray(),
            Genres = metadata.Genres.ToArray(),
            Annotation = metadata.Annotation,
            Series = metadata.Series,
            SeriesNumber = metadata.SeriesNumber,
            Language = metadata.Language,
            PublishYear = metadata.PublishYear,
            CoverReference = metadata.CoverReference,
            CoverImageBytes = coverBytes,
            ThumbnailBytes = options.ExtractThumbnail ? CreateThumbnailBytes(coverBytes) : null
        });
    }

    private static void ParseTitleInfo(
        XmlReader reader,
        MutableMetadata metadata,
        Fb2ParsingOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var titleInfoXml = reader.ReadOuterXml();
        if (string.IsNullOrWhiteSpace(titleInfoXml))
        {
            return;
        }

        var titleInfo = XElement.Parse(titleInfoXml);
        metadata.Title ??= NullIfWhiteSpace(titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "book-title")?.Value);

        foreach (var authorElement in titleInfo.Elements().Where(element => element.Name.LocalName == "author"))
        {
            var author = ParseAuthor(authorElement, cancellationToken);
            if (!string.IsNullOrWhiteSpace(author) &&
                !metadata.Authors.Contains(author, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Authors.Add(author);
            }
        }

        foreach (var genreElement in titleInfo.Elements().Where(element => element.Name.LocalName == "genre"))
        {
            var genre = NullIfWhiteSpace(genreElement.Value);
            if (!string.IsNullOrWhiteSpace(genre) &&
                !metadata.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Genres.Add(genre);
            }
        }

        if (options.ExtractAnnotation)
        {
            metadata.Annotation ??= ParseAnnotation(
                titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "annotation"),
                cancellationToken);
        }

        var sequenceElement = titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "sequence");
        metadata.Series ??= NullIfWhiteSpace(sequenceElement?.Attribute("name")?.Value);
        metadata.SeriesNumber ??= TryParseInteger(sequenceElement?.Attribute("number")?.Value);
        metadata.Language ??= NullIfWhiteSpace(titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "lang")?.Value);

        var dateElement = titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "date");
        metadata.PublishYear ??= TryParseYear(dateElement?.Attribute("value")?.Value)
                                 ?? TryParseYear(dateElement?.Value);

        if (options.ExtractCoverImages)
        {
            metadata.CoverReference ??= ParseCoverReference(
                titleInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "coverpage"));
        }
    }

    private static string? ParseAuthor(XElement authorElement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parts = new[]
            {
                authorElement.Elements().FirstOrDefault(element => element.Name.LocalName == "first-name")?.Value,
                authorElement.Elements().FirstOrDefault(element => element.Name.LocalName == "middle-name")?.Value,
                authorElement.Elements().FirstOrDefault(element => element.Name.LocalName == "last-name")?.Value
            }
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        if (parts.Length > 0)
        {
            return string.Join(" ", parts);
        }

        return NullIfWhiteSpace(authorElement.Elements().FirstOrDefault(element => element.Name.LocalName == "nickname")?.Value);
    }

    private static string? ParseAnnotation(XElement? annotationElement, CancellationToken cancellationToken)
    {
        var blocks = new List<string>();
        if (annotationElement is null)
        {
            return null;
        }

        foreach (var element in annotationElement.DescendantsAndSelf()
                     .Where(element => element.Name.LocalName is "p" or "subtitle" or "title"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = NormalizeWhitespace(element.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                blocks.Add(value);
            }
        }

        if (blocks.Count > 0)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, blocks);
        }

        return null;
    }

    private static string? ParseCoverReference(XElement? coverPageElement)
    {
        if (coverPageElement is null)
        {
            return null;
        }

        var imageElement = coverPageElement.Elements().FirstOrDefault(element => element.Name.LocalName == "image");
        return NullIfWhiteSpace(imageElement?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "href")?.Value);
    }

    private static void ParsePublishInfo(
        XmlReader reader,
        MutableMetadata metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var publishInfoXml = reader.ReadOuterXml();
        if (string.IsNullOrWhiteSpace(publishInfoXml))
        {
            return;
        }

        var publishInfo = XElement.Parse(publishInfoXml);
        metadata.PublishYear ??= TryParseYear(publishInfo.Elements().FirstOrDefault(element => element.Name.LocalName == "year")?.Value);
    }

    private static void TryReadCoverBinary(XmlReader reader, MutableMetadata metadata)
    {
        if (metadata.CoverImageBytes is not null)
        {
            reader.Skip();
            return;
        }

        var referenceId = NormalizeBinaryId(metadata.CoverReference);
        var binaryId = NormalizeBinaryId(reader.GetAttribute("id"));
        if (referenceId is null ||
            binaryId is null ||
            !string.Equals(referenceId, binaryId, StringComparison.OrdinalIgnoreCase))
        {
            reader.Skip();
            return;
        }

        try
        {
            var payload = RemoveWhitespace(reader.ReadElementContentAsString());
            metadata.CoverImageBytes = payload.Length == 0
                ? null
                : Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            metadata.CoverImageBytes = null;
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

    private static string RemoveWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string? NormalizeBinaryId(string? value)
    {
        var normalized = NullIfWhiteSpace(value);
        return normalized?.TrimStart('#');
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
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

    private sealed class MutableMetadata
    {
        public string? Title { get; set; }
        public List<string> Authors { get; } = [];
        public List<string> Genres { get; } = [];
        public string? Annotation { get; set; }
        public string? Series { get; set; }
        public int? SeriesNumber { get; set; }
        public string? Language { get; set; }
        public int? PublishYear { get; set; }
        public string? CoverReference { get; set; }
        public byte[]? CoverImageBytes { get; set; }
    }
}
