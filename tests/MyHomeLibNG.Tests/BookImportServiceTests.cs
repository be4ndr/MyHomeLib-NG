using Microsoft.Data.Sqlite;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Import;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using MyHomeLibNG.Infrastructure.Repositories;
using Xunit;
using System.IO.Compression;

namespace MyHomeLibNG.Tests;

public sealed class BookImportServiceTests
{
    [Fact]
    public async Task ImportLibraryAsync_ImportsValidEntriesAndContinuesOnMalformedFb2()
    {
        const string archivePath = "/library/archives/import.zip";
        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("books/good.fb2", Fb2ImportTestData.CreateFb2(
                title: "Imported Title",
                authors: ["Ada Author"],
                genres: ["fantasy", "adventure"],
                annotationParagraphs: ["A short annotation."],
                seriesName: "Importer Saga",
                seriesNumber: 3,
                language: "en",
                publishYear: 2020,
                coverReference: "#cover.png",
                coverImageBytes: Fb2ImportTestData.TinyPngBytes)),
            ("books/bad.fb2", "<broken")));

        using var database = await CreateDatabaseAsync();
        var service = new BookImportService(
            new ZipArchiveScanner(fileSystem),
            new Fb2MetadataParser(),
            new SqliteLibraryRepository(database.ConnectionString));

        var summary = await service.ImportLibraryAsync(CreateProfile(), archivePath);

        Assert.Equal(1, summary.ArchivesScanned);
        Assert.Equal(2, summary.EntriesDiscovered);
        Assert.Equal(1, summary.ImportedCount);
        Assert.Equal(1, summary.BooksAdded);
        Assert.Equal(0, summary.BooksUpdated);
        Assert.Equal(0, summary.BooksSkipped);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal("books/bad.fb2", summary.Failures[0].EntryPath);

        var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
        var title = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books;");
        var authors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books;");
        var series = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Series FROM Books;");
        var genres = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Genres FROM Books;");
        var hash = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT ContentHash FROM Books;");
        var thumbnailIsNull = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT CASE WHEN CoverThumbnail IS NULL THEN 1 ELSE 0 END FROM Books;");

        Assert.Equal(1L, rowCount);
        Assert.Equal("Imported Title", title);
        Assert.Equal("Ada Author", authors);
        Assert.Equal("Importer Saga", series);
        Assert.Equal("fantasy; adventure", genres);
        Assert.Equal(64, hash.Length);
        Assert.Equal(1L, thumbnailIsNull);
    }

    [Fact]
    public async Task ImportLibraryAsync_UpsertsExistingArchiveEntry()
    {
        const string archivePath = "/library/archives/import.zip";
        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("books/book.fb2", Fb2ImportTestData.CreateFb2(
                title: "First Title",
                authors: ["First Author"],
                annotationParagraphs: ["First annotation."],
                publishYear: 2021))));

        using var database = await CreateDatabaseAsync();
        var service = new BookImportService(
            new ZipArchiveScanner(fileSystem),
            new Fb2MetadataParser(),
            new SqliteLibraryRepository(database.ConnectionString));

        var firstSummary = await service.ImportLibraryAsync(CreateProfile(), archivePath);

        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("books/book.fb2", Fb2ImportTestData.CreateFb2(
                title: "Updated Title",
                authors: ["Second Author"],
                annotationParagraphs: ["Updated annotation."],
                publishYear: 2022))));

        var secondSummary = await service.ImportLibraryAsync(CreateProfile(), archivePath);

        var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
        var title = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books;");
        var authors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books;");
        var year = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT PublishYear FROM Books;");

        Assert.Equal(1, firstSummary.BooksAdded);
        Assert.Equal(1, secondSummary.BooksUpdated);
        Assert.Equal(1L, rowCount);
        Assert.Equal("Updated Title", title);
        Assert.Equal("Second Author", authors);
        Assert.Equal(2022L, year);
    }

    [Fact]
    public async Task ImportLibraryAsync_UsesInpxAsPrimarySourceWithoutZipParsing()
    {
        const string inpxPath = "/library/catalog.inpx";
        const string archiveRoot = "/library/archives";
        var fileSystem = new InMemoryOfflineLibraryFileSystem();
        fileSystem.AddFile(inpxPath, BuildInpxArchive(
            "d.fb2-000001-000100.inp",
            "Doe,John:\u0004fantasy:adventure:\u0004Inpx Imported One\u0004Series One\u00041\u00041001\u00042048\u00041001\u00040\u0004fb2\u00042025-01-01\u0004en\u00040\u0004\u0004\n" +
            "Doe,Jane:\u0004history:\u0004Inpx Imported Two\u0004\u0004\u00041002\u00041024\u00041002\u00040\u0004fb2\u00042024-06-15\u0004ru\u00040\u0004\u0004"));

        using var database = await CreateDatabaseAsync();
        var repository = new SqliteLibraryRepository(database.ConnectionString);
        var inpxReader = new InpxLibraryIndexReader(new InpxCatalogParser(), fileSystem);
        var service = new BookImportService(
            new ThrowingZipArchiveScanner(),
            new Fb2MetadataParser(),
            repository,
            inpxLibraryIndexReader: inpxReader);

        var profile = new LibraryProfile
        {
            Id = 41,
            Name = "Import tests",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = inpxPath,
                ArchiveDirectoryPath = archiveRoot
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var summary = await service.ImportLibraryAsync(profile);

        Assert.Equal(2, summary.EntriesDiscovered);
        Assert.Equal(2, summary.ImportedCount);
        Assert.Equal(2, summary.BooksAdded);
        Assert.Equal(0, summary.FailedCount);

        var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
        var title = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books WHERE EntryPath = '1001.fb2';");
        var authors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books WHERE EntryPath = '1001.fb2';");
        var series = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Series FROM Books WHERE EntryPath = '1001.fb2';");
        var seriesNumber = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT SeriesNumber FROM Books WHERE EntryPath = '1001.fb2';");
        var genres = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Genres FROM Books WHERE EntryPath = '1001.fb2';");
        var language = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Language FROM Books WHERE EntryPath = '1001.fb2';");
        var year = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT PublishYear FROM Books WHERE EntryPath = '1001.fb2';");
        var archivePath = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT ArchivePath FROM Books WHERE EntryPath = '1001.fb2';");
        var fileSize = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT FileSize FROM Books WHERE EntryPath = '1001.fb2';");
        var libId = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT LibId FROM Books WHERE EntryPath = '1001.fb2';");

        Assert.Equal(2L, rowCount);
        Assert.Equal("Inpx Imported One", title);
        Assert.Equal("Doe John", authors);
        Assert.Equal("Series One", series);
        Assert.Equal(1L, seriesNumber);
        Assert.Equal("fantasy; adventure", genres);
        Assert.Equal("en", language);
        Assert.Equal(2025L, year);
        Assert.Equal(Path.Combine(archiveRoot, "d.fb2-000001-000100.zip"), archivePath);
        Assert.Equal(2048L, fileSize);
        Assert.Equal("1001", libId);
    }

    private static LibraryProfile CreateProfile()
    {
        return new LibraryProfile
        {
            Id = 41,
            Name = "Import tests",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = "/library/catalog.inpx",
                ArchiveDirectoryPath = "/library/archives"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static byte[] BuildInpxArchive(string inpName, string inpContent)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var structure = archive.CreateEntry("structure.info");
            using (var structureWriter = new StreamWriter(structure.Open()))
            {
                structureWriter.Write("authors,genres,title,series,series_number,source_id,file_size,lib_id,deleted,format,updated_at,language,rating,keywords,identifier");
            }

            var data = archive.CreateEntry(inpName);
            using var dataWriter = new StreamWriter(data.Open());
            dataWriter.Write(inpContent);
        }

        return memory.ToArray();
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-import-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";
        var initializer = new SqliteSchemaInitializer(connectionString);
        await initializer.InitializeAsync();
        return new TestDatabase(dbPath, connectionString);
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private sealed class TestDatabase : IDisposable
    {
        public TestDatabase(string databasePath, string connectionString)
        {
            DatabasePath = databasePath;
            ConnectionString = connectionString;
        }

        public string DatabasePath { get; }
        public string ConnectionString { get; }

        public void Dispose()
        {
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }
    }

    private sealed class ThrowingZipArchiveScanner : IZipArchiveScanner
    {
        public async IAsyncEnumerable<ZipArchiveBookEntry> ScanAsync(
            string path,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(path))
            {
                yield break;
            }

            throw new InvalidOperationException("ZIP scanner should not be used for INPX-first import.");
        }

        public IReadOnlyList<string> ResolveArchivePaths(string path)
            => throw new InvalidOperationException("ZIP scanner should not be used for INPX-first import.");

        public Task ScanArchiveAsync(
            string archivePath,
            Func<ZipArchiveBookEntry, CancellationToken, ValueTask> onEntry,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("ZIP scanner should not be used for INPX-first import.");
    }
}
