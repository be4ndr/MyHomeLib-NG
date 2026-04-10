using Microsoft.Data.Sqlite;
using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Import;
using MyHomeLibNG.Infrastructure.Repositories;
using Xunit;

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
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal("books/bad.fb2", summary.Failures[0].EntryPath);

        var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
        var title = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books;");
        var authors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books;");
        var series = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Series FROM Books;");
        var genres = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Genres FROM Books;");
        var hash = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT ContentHash FROM Books;");
        var thumbnailLength = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT length(CoverThumbnail) FROM Books;");

        Assert.Equal(1L, rowCount);
        Assert.Equal("Imported Title", title);
        Assert.Equal("Ada Author", authors);
        Assert.Equal("Importer Saga", series);
        Assert.Equal("fantasy; adventure", genres);
        Assert.Equal(64, hash.Length);
        Assert.True(thumbnailLength > 0);
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

        await service.ImportLibraryAsync(CreateProfile(), archivePath);

        fileSystem.AddFile(archivePath, Fb2ImportTestData.CreateZipArchive(
            ("books/book.fb2", Fb2ImportTestData.CreateFb2(
                title: "Updated Title",
                authors: ["Second Author"],
                annotationParagraphs: ["Updated annotation."],
                publishYear: 2022))));

        await service.ImportLibraryAsync(CreateProfile(), archivePath);

        var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
        var title = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books;");
        var authors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books;");
        var year = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT PublishYear FROM Books;");

        Assert.Equal(1L, rowCount);
        Assert.Equal("Updated Title", title);
        Assert.Equal("Second Author", authors);
        Assert.Equal(2022L, year);
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
}
