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
            "structure.info", "source_id,title,authors,language,description,subjects,publisher,published_year,isbn10,isbn13,container_path,archive_entry_path",
            "catalog.inp", "id-1\u0004Book One\u0004Jane Doe\u0004en\u0004Desc\u0004Subject\u0004Press\u00042024\u0004\u0004\u0004folder/book-one.fb2\u0004\n" +
                           "id-1\u0004Duplicate\u0004Jane Doe\u0004en\u0004Desc\u0004Subject\u0004Press\u00042024\u0004\u0004\u0004folder/book-one.fb2\u0004\n" +
                           "broken-row"));

        var results = await parser.ParseAsync(stream, "Offline Fixture");

        Assert.Single(results);
        Assert.Equal("id-1", results[0].Book.SourceId);
        Assert.Equal("Book One", results[0].Book.Title);
    }

    [Fact]
    public async Task ParseAsync_ReturnsEmpty_WhenArchiveIsInvalid()
    {
        var parser = new InpxCatalogParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not-a-zip"));

        var results = await parser.ParseAsync(stream, "Offline Fixture");

        Assert.Empty(results);
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
