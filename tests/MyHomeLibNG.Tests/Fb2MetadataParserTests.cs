using System.Text;
using MyHomeLibNG.Infrastructure.Import;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class Fb2MetadataParserTests
{
    private readonly Fb2MetadataParser _parser = new();

    [Fact]
    public async Task ParseAsync_ExtractsAnnotationAndCoreMetadata()
    {
        var fb2 = Fb2ImportTestData.CreateFb2(
            title: "The Trial",
            authors: ["Franz Kafka", "Max Brod"],
            genres: ["classic", "fiction"],
            annotationParagraphs: ["First paragraph.", "Second paragraph."],
            language: "de",
            publishYear: 1925);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fb2));
        var metadata = await _parser.ParseAsync(stream);

        Assert.Equal("The Trial", metadata.Title);
        Assert.Equal(["Franz Kafka", "Max Brod"], metadata.Authors);
        Assert.Equal(["classic", "fiction"], metadata.Genres);
        Assert.Equal($"First paragraph.{Environment.NewLine}{Environment.NewLine}Second paragraph.", metadata.Annotation);
        Assert.Equal("de", metadata.Language);
        Assert.Equal(1925, metadata.PublishYear);
    }

    [Fact]
    public async Task ParseAsync_ExtractsSeriesMetadata()
    {
        var fb2 = Fb2ImportTestData.CreateFb2(
            title: "Volume Two",
            seriesName: "Chronicles",
            seriesNumber: 2);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fb2));
        var metadata = await _parser.ParseAsync(stream);

        Assert.Equal("Chronicles", metadata.Series);
        Assert.Equal(2, metadata.SeriesNumber);
    }

    [Fact]
    public async Task ParseAsync_ExtractsCoverImageAndThumbnail()
    {
        var fb2 = Fb2ImportTestData.CreateFb2(
            title: "Covered Book",
            coverReference: "#cover.png",
            coverImageBytes: Fb2ImportTestData.TinyPngBytes);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fb2));
        var metadata = await _parser.ParseAsync(stream);

        Assert.Equal("#cover.png", metadata.CoverReference);
        Assert.NotNull(metadata.CoverImageBytes);
        Assert.NotEmpty(metadata.CoverImageBytes!);
        Assert.NotNull(metadata.ThumbnailBytes);
        Assert.NotEmpty(metadata.ThumbnailBytes!);
    }

    [Fact]
    public async Task ParseAsync_IgnoresMalformedCoverPayload()
    {
        var fb2 = Fb2ImportTestData.CreateFb2(
            title: "Broken Cover",
            coverReference: "#cover.png",
            malformedCoverPayload: "not-base64");

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fb2));
        var metadata = await _parser.ParseAsync(stream);

        Assert.Equal("Broken Cover", metadata.Title);
        Assert.Null(metadata.CoverImageBytes);
        Assert.Null(metadata.ThumbnailBytes);
    }
}
