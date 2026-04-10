using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;

namespace MyHomeLibNG.Tests;

internal static class Fb2ImportTestData
{
    private static readonly XNamespace Fb2Namespace = "http://www.gribuser.ru/xml/fictionbook/2.0";
    private static readonly XNamespace XLinkNamespace = "http://www.w3.org/1999/xlink";

    public static byte[] TinyPngBytes { get; } = CreateTinyPngBytes();

    public static string CreateFb2(
        string title = "Sample Book",
        IReadOnlyList<string>? authors = null,
        IReadOnlyList<string>? genres = null,
        IReadOnlyList<string>? annotationParagraphs = null,
        string? seriesName = null,
        int? seriesNumber = null,
        string? language = "en",
        int? publishYear = 2024,
        string? coverReference = null,
        byte[]? coverImageBytes = null,
        string? malformedCoverPayload = null)
    {
        var titleInfo = new XElement(Fb2Namespace + "title-info",
            new XElement(Fb2Namespace + "book-title", title));

        foreach (var author in authors ?? ["Ada Author"])
        {
            titleInfo.Add(CreateAuthor(author));
        }

        foreach (var genre in genres ?? ["fantasy"])
        {
            titleInfo.Add(new XElement(Fb2Namespace + "genre", genre));
        }

        if (annotationParagraphs is not null)
        {
            titleInfo.Add(new XElement(
                Fb2Namespace + "annotation",
                annotationParagraphs.Select(paragraph => new XElement(Fb2Namespace + "p", paragraph))));
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            titleInfo.Add(new XElement(Fb2Namespace + "lang", language));
        }

        if (publishYear.HasValue)
        {
            titleInfo.Add(new XElement(Fb2Namespace + "date",
                new XAttribute("value", $"{publishYear:0000}-01-01"),
                publishYear.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(seriesName))
        {
            var sequence = new XElement(Fb2Namespace + "sequence", new XAttribute("name", seriesName));
            if (seriesNumber.HasValue)
            {
                sequence.Add(new XAttribute("number", seriesNumber.Value));
            }

            titleInfo.Add(sequence);
        }

        if (!string.IsNullOrWhiteSpace(coverReference))
        {
            titleInfo.Add(new XElement(
                Fb2Namespace + "coverpage",
                new XElement(
                    Fb2Namespace + "image",
                    new XAttribute(XLinkNamespace + "href", coverReference))));
        }

        var document = new XDocument(
            new XElement(Fb2Namespace + "FictionBook",
                new XAttribute(XNamespace.Xmlns + "l", XLinkNamespace),
                new XElement(Fb2Namespace + "description",
                    titleInfo,
                    new XElement(Fb2Namespace + "publish-info",
                        publishYear.HasValue ? new XElement(Fb2Namespace + "year", publishYear.Value.ToString()) : null))));

        var binaryId = NormalizeBinaryId(coverReference);
        if (binaryId is not null)
        {
            document.Root!.Add(new XElement(
                Fb2Namespace + "binary",
                new XAttribute("id", binaryId),
                malformedCoverPayload ?? Convert.ToBase64String(coverImageBytes ?? TinyPngBytes)));
        }

        using var writer = new Utf8StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    public static byte[] CreateZipArchive(params (string Path, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.Path);
                using var writer = new StreamWriter(zipEntry.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write(entry.Content);
            }
        }

        return memory.ToArray();
    }

    private static XElement CreateAuthor(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => new XElement(Fb2Namespace + "author"),
            1 => new XElement(Fb2Namespace + "author",
                new XElement(Fb2Namespace + "nickname", parts[0])),
            2 => new XElement(Fb2Namespace + "author",
                new XElement(Fb2Namespace + "first-name", parts[0]),
                new XElement(Fb2Namespace + "last-name", parts[1])),
            _ => new XElement(Fb2Namespace + "author",
                new XElement(Fb2Namespace + "first-name", parts[0]),
                new XElement(Fb2Namespace + "middle-name", string.Join(" ", parts.Skip(1).Take(parts.Length - 2))),
                new XElement(Fb2Namespace + "last-name", parts[^1]))
        };
    }

    private static string? NormalizeBinaryId(string? coverReference)
        => string.IsNullOrWhiteSpace(coverReference) ? null : coverReference.Trim().TrimStart('#');

    private static byte[] CreateTinyPngBytes()
    {
        using var surface = SKSurface.Create(new SKImageInfo(24, 36));
        surface.Canvas.Clear(new SKColor(245, 236, 210));
        using var paint = new SKPaint
        {
            Color = new SKColor(73, 91, 122),
            IsAntialias = true
        };
        surface.Canvas.DrawRect(new SKRect(3, 3, 21, 33), paint);
        surface.Canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
