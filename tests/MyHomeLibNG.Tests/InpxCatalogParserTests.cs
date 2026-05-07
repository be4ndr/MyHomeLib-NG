using System.IO.Compression;
using System.Text;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class InpxCatalogParserTests
{
    [Fact]
    public async Task ParseAsync_SkipsMalformedRows_AndDeduplicatesSourceIds()
    {
        var parser = new InpxCatalogParser();
        await using var stream = new MemoryStream(BuildArchive(
            "structure.info", "source_id,title,authors,language,description,subjects,publisher,published_year,isbn10,isbn13,container_path,archive_entry_path,series,series_number,file_size,lib_id",
            "catalog.inp", "id-1\u0004Book One\u0004Jane Doe\u0004en\u0004Desc\u0004Subject\u0004Press\u00042024\u0004\u0004\u0004books.zip\u0004folder/book-one.fb2\u0004Saga\u00042\u0004512\u0004lib-42\n" +
                           "id-1\u0004Duplicate\u0004Jane Doe\u0004en\u0004Desc\u0004Subject\u0004Press\u00042024\u0004\u0004\u0004books.zip\u0004folder/book-one.fb2\u0004Saga\u00042\u0004512\u0004lib-42\n" +
                           "broken-row"));

        var results = await parser.ParseAsync(stream, "Offline Fixture");

        Assert.Single(results);
        Assert.Equal("id-1", results[0].Book.SourceId);
        Assert.Equal("Book One", results[0].Book.Title);
        Assert.Equal("Saga", results[0].Book.Series);
        Assert.Equal(2, results[0].SeriesNumber);
        Assert.Equal(512, results[0].FileSize);
        Assert.Equal("lib-42", results[0].LibId);
        Assert.Equal("books.zip", results[0].ContainerPath);
        Assert.Equal("folder/book-one.fb2", results[0].ArchiveEntryPath);
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmpty_WhenArchiveIsInvalid()
    {
        var parser = new InpxCatalogParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-zip"));

        var results = await parser.ParseAsync(stream, "Offline Fixture");

        Assert.Empty(results);
    }

    [Fact]
    public async Task ParseAsync_ParsesLegacyFlibustaRowsWithoutStructureInfo()
    {
        var parser = new InpxCatalogParser();
        await using var stream = new MemoryStream(BuildArchive(
            "collection.info", "fixture",
            "d.fb2-000001-000100.inp", "Surname,Name,Patronymic:\u0004sf_fantasy:\u0004Legacy Title\u0004Legacy Series\u00042\u0004110119\u00041230745\u0004110119\u00040\u0004fb2\u00042008-07-05\u0004ru\u00043\u0004\u0004"));

        var results = await parser.ParseAsync(stream, "Offline Fixture");

        var result = Assert.Single(results);
        Assert.Equal("110119", result.Book.SourceId);
        Assert.Equal("Legacy Title", result.Book.Title);
        Assert.Equal(["Surname Name Patronymic"], result.Book.Authors);
        Assert.Equal("Legacy Series", result.Book.Series);
        Assert.Equal(2, result.SeriesNumber);
        Assert.Equal(1230745, result.FileSize);
        Assert.Equal("110119", result.LibId);
        Assert.Equal(2008, result.Book.PublishedYear);
        Assert.Equal(["sf_fantasy"], result.Book.Subjects);
        Assert.Equal("ru", result.Book.Language);
        Assert.Equal("d.fb2-000001-000100.zip", result.ContainerPath);
        Assert.Equal("110119.fb2", result.ArchiveEntryPath);
        Assert.Equal(["fb2"], result.Book.Formats);
    }

    private static byte[] BuildArchive(string structureName, string structureContent, string dataName, string dataContent)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var structure = archive.CreateEntry(structureName);
            using (var writer = new StreamWriter(structure.Open(), Encoding.UTF8))
            {
                writer.Write(structureContent);
            }

            var data = archive.CreateEntry(dataName);
            using var dataWriter = new StreamWriter(data.Open(), Encoding.UTF8);
            dataWriter.Write(dataContent);
        }

        return memory.ToArray();
    }
}
