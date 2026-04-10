using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Repositories;
using MyHomeLibNG.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class SqliteLibraryRepositoryTests
{
    private const string MockRootDirectory = @"X:\mock-library";
    private const string MockInpxFilePath = @"X:\mock-library\sample.inpx";

    [Fact]
    public async Task AddAndGetById_PersistsOnlineLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var createdAtUtc = DateTimeOffset.UtcNow;
            var newId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Flibusta",
                ProviderId = BookProviderIds.ProjectGutenberg,
                LibraryType = LibraryType.Online,
                OnlineSource = new OnlineLibrarySourceSettings
                {
                    ApiBaseUrl = "https://flibusta.lib/api",
                    SearchEndpoint = "https://flibusta.lib/api/search"
                },
                CreatedAtUtc = createdAtUtc
            });

            var loaded = await repository.GetByIdAsync(newId);

            Assert.NotNull(loaded);
            Assert.Equal("Flibusta", loaded.Name);
            Assert.Equal(LibraryType.Online, loaded.LibraryType);
            Assert.NotNull(loaded.OnlineSource);
            Assert.Equal("https://flibusta.lib/api", loaded.OnlineSource.ApiBaseUrl);
            Assert.Equal("https://flibusta.lib/api/search", loaded.OnlineSource.SearchEndpoint);
            Assert.Null(loaded.FolderSource);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task AddAndGetById_PersistsFolderLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var newId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Local archive",
                ProviderId = BookProviderIds.OfflineInpx,
                LibraryType = LibraryType.Folder,
                FolderSource = new FolderLibrarySourceSettings
                {
                    InpxFilePath = MockInpxFilePath,
                    ArchiveDirectoryPath = MockRootDirectory
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            var loaded = await repository.GetByIdAsync(newId);

            Assert.NotNull(loaded);
            Assert.Equal(LibraryType.Folder, loaded.LibraryType);
            Assert.NotNull(loaded.FolderSource);
            Assert.Equal(MockInpxFilePath, loaded.FolderSource.InpxFilePath);
            Assert.Equal(MockRootDirectory, loaded.FolderSource.ArchiveDirectoryPath);
            Assert.Null(loaded.OnlineSource);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var libraryId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Delete me",
                ProviderId = BookProviderIds.ProjectGutenberg,
                LibraryType = LibraryType.Online,
                OnlineSource = new OnlineLibrarySourceSettings
                {
                    ApiBaseUrl = "https://example.test/api",
                    SearchEndpoint = "https://example.test/api/search"
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await repository.DeleteAsync(libraryId);

            var profiles = await repository.GetLibraryProfilesAsync();
            var loaded = await repository.GetByIdAsync(libraryId);

            Assert.DoesNotContain(profiles, profile => profile.Id == libraryId);
            Assert.Null(loaded);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesBooksSchemaWithRequiredColumns()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var columns = await GetTableColumnsAsync(database.ConnectionString, "Books");

            Assert.Contains("LibraryProfileId", columns);
            Assert.Contains("Title", columns);
            Assert.Contains("Authors", columns);
            Assert.Contains("Annotation", columns);
            Assert.Contains("PublishYear", columns);
            Assert.Contains("PrimaryFormat", columns);
            Assert.Contains("Series", columns);
            Assert.Contains("SeriesNumber", columns);
            Assert.Contains("Genres", columns);
            Assert.Contains("Language", columns);
            Assert.Contains("ArchivePath", columns);
            Assert.Contains("EntryPath", columns);
            Assert.Contains("FileName", columns);
            Assert.Contains("FileSize", columns);
            Assert.Contains("ContentHash", columns);
            Assert.Contains("CoverThumbnail", columns);
            Assert.Contains("CreatedAt", columns);
            Assert.Contains("UpdatedAt", columns);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task BooksUniqueIndex_PreventsDuplicateLogicalRows()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            const string duplicateKeyArchivePath = @"X:\mock-library\archives\a.zip";
            const string duplicateKeyEntryPath = "books/book.fb2";

            await using var connection = new SqliteConnection(database.ConnectionString);
            await connection.OpenAsync();

            await InsertBookRowAsync(connection, 17, duplicateKeyArchivePath, duplicateKeyEntryPath, "First title");

            var exception = await Assert.ThrowsAsync<SqliteException>(() =>
                InsertBookRowAsync(connection, 17, duplicateKeyArchivePath, duplicateKeyEntryPath, "Duplicate title"));

            Assert.Equal(19, exception.SqliteErrorCode);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task UpsertImportedBookAsync_UpdatesExistingBookInsteadOfCreatingDuplicate()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var createdAt = new DateTimeOffset(2026, 4, 9, 12, 0, 0, TimeSpan.Zero);
            var initialUpdatedAt = createdAt.AddMinutes(5);
            var finalUpdatedAt = createdAt.AddMinutes(30);

            var firstId = await repository.UpsertImportedBookAsync(new BookImportRecord
            {
                LibraryProfileId = 23,
                Title = "Original title",
                Authors = "Author One",
                Annotation = "Original annotation",
                PublishYear = 2001,
                PrimaryFormat = FileFormat.Fb2,
                Series = "Series A",
                SeriesNumber = 1,
                Genres = "fantasy;adventure",
                Language = "ru",
                ArchivePath = @"X:\mock-library\archives\set-01.zip",
                EntryPath = "books/original.fb2",
                FileName = "original.fb2",
                FileSize = 111,
                ContentHash = "hash-1",
                CoverThumbnail = [1, 2, 3],
                CreatedAt = createdAt,
                UpdatedAt = initialUpdatedAt
            });

            var secondId = await repository.UpsertImportedBookAsync(new BookImportRecord
            {
                LibraryProfileId = 23,
                Title = "Updated title",
                Authors = "Author Two",
                Annotation = "Updated annotation",
                PublishYear = 2002,
                PrimaryFormat = FileFormat.Fb2,
                Series = "Series B",
                SeriesNumber = 2,
                Genres = "science-fiction",
                Language = "en",
                ArchivePath = @"X:\mock-library\archives\set-01.zip",
                EntryPath = "books/original.fb2",
                FileName = "updated.fb2",
                FileSize = 222,
                ContentHash = "hash-2",
                CoverThumbnail = [9, 8, 7],
                CreatedAt = createdAt.AddDays(1),
                UpdatedAt = finalUpdatedAt
            });

            var rowCount = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT COUNT(*) FROM Books;");
            var storedTitle = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Title FROM Books;");
            var storedAuthors = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Authors FROM Books;");
            var storedSeries = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT Series FROM Books;");
            var storedCreatedAt = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT CreatedAt FROM Books;");
            var storedUpdatedAt = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT UpdatedAt FROM Books;");
            var storedFileName = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT FileName FROM Books;");
            var storedFileSize = await ExecuteScalarAsync<long>(database.ConnectionString, "SELECT FileSize FROM Books;");
            var storedContentHash = await ExecuteScalarAsync<string>(database.ConnectionString, "SELECT ContentHash FROM Books;");

            Assert.Equal(firstId, secondId);
            Assert.Equal(1L, rowCount);
            Assert.Equal("Updated title", storedTitle);
            Assert.Equal("Author Two", storedAuthors);
            Assert.Equal("Series B", storedSeries);
            Assert.Equal(createdAt.ToString("O"), storedCreatedAt);
            Assert.Equal(finalUpdatedAt.ToString("O"), storedUpdatedAt);
            Assert.Equal("updated.fb2", storedFileName);
            Assert.Equal(222L, storedFileSize);
            Assert.Equal("hash-2", storedContentHash);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task GetImportedBookMetadataAsync_ReturnsStoredIndexedMetadata()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            await repository.UpsertImportedBookAsync(new BookImportRecord
            {
                LibraryProfileId = 55,
                Title = "Indexed title",
                Authors = "Indexed Author",
                Annotation = "Indexed annotation",
                PublishYear = 1999,
                PrimaryFormat = FileFormat.Fb2,
                Series = "Indexed series",
                Genres = "history; memoir",
                Language = "ru",
                ArchivePath = @"X:\mock-library\archives\indexed.zip",
                EntryPath = "books/indexed.fb2",
                FileName = "indexed.fb2",
                FileSize = 123,
                ContentHash = "hash-indexed",
                CoverThumbnail = [7, 8, 9],
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var metadata = await repository.GetImportedBookMetadataAsync(
                55,
                @"X:\mock-library\archives\indexed.zip",
                "books/indexed.fb2");

            Assert.NotNull(metadata);
            Assert.Equal("Indexed title", metadata!.Title);
            Assert.Equal("Indexed Author", metadata.Authors);
            Assert.Equal("Indexed annotation", metadata.Annotation);
            Assert.Equal(1999, metadata.PublishYear);
            Assert.Equal("Indexed series", metadata.Series);
            Assert.Equal("history; memoir", metadata.Genres);
            Assert.Equal("ru", metadata.Language);
            Assert.Equal("hash-indexed", metadata.ContentHash);
            Assert.Equal([7, 8, 9], metadata.CoverThumbnail);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOnlineApiStructure()
    {
        var resolver = new LibrarySourceResolver(CreateOnlineEnvironment());
        var structure = await resolver.ResolveAsync(CreateOnlineProfile());

        Assert.Equal(LibraryType.Online, structure.LibraryType);
        Assert.Equal(2, structure.Sources.Count);
        Assert.All(structure.Sources, source => Assert.Equal(SourceKind.Http, source.Kind));
        Assert.All(structure.Sources, source => Assert.True(source.Exists));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOfflineInpxAndArchiveStructure()
    {
        var resolver = new LibrarySourceResolver(CreateOfflineEnvironment());
        var structure = await resolver.ResolveAsync(CreateFolderProfile());

        Assert.Equal(LibraryType.Folder, structure.LibraryType);
        Assert.Equal(4, structure.Sources.Count);

        var inpxSource = structure.Sources.Single(source => source.Description == "Offline INPX index");
        Assert.Equal(SourceKind.FileSystem, inpxSource.Kind);
        Assert.True(inpxSource.Exists);
        Assert.Equal(100, inpxSource.SizeBytes);

        var archiveSources = structure.Sources.Where(source => source.Kind == SourceKind.Archive).ToArray();
        Assert.Equal(3, archiveSources.Length);
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".gz", StringComparison.OrdinalIgnoreCase));
        Assert.All(archiveSources, source => Assert.True(source.Exists));
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-test-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";
        var initializer = new SqliteSchemaInitializer(connectionString);
        await initializer.InitializeAsync();
        return new TestDatabase(dbPath, connectionString);
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(string connectionString, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task InsertBookRowAsync(
        SqliteConnection connection,
        long libraryProfileId,
        string archivePath,
        string entryPath,
        string title)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO Books(
                                  LibraryProfileId,
                                  Title,
                                  PrimaryFormat,
                                  ArchivePath,
                                  EntryPath,
                                  CreatedAt,
                                  UpdatedAt)
                              VALUES (
                                  $libraryProfileId,
                                  $title,
                                  $primaryFormat,
                                  $archivePath,
                                  $entryPath,
                                  $createdAt,
                                  $updatedAt);
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", libraryProfileId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$primaryFormat", (int)FileFormat.Fb2);
        command.Parameters.AddWithValue("$archivePath", archivePath);
        command.Parameters.AddWithValue("$entryPath", entryPath);
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
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

    private static LibraryProfile CreateOnlineProfile()
    {
        return new LibraryProfile
        {
            Id = 17,
            Name = "Project Gutenberg",
            ProviderId = BookProviderIds.ProjectGutenberg,
            LibraryType = LibraryType.Online,
            OnlineSource = new OnlineLibrarySourceSettings
            {
                ApiBaseUrl = "https://gutendex.com",
                SearchEndpoint = "https://gutendex.com/books"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static LibraryProfile CreateFolderProfile()
    {
        return new LibraryProfile
        {
            Id = 42,
            Name = "Offline sample",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = MockInpxFilePath,
                ArchiveDirectoryPath = MockRootDirectory
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static MockLibrarySourceEnvironment CreateOnlineEnvironment()
    {
        var environment = new MockLibrarySourceEnvironment();
        environment.ValidUris.Add("https://gutendex.com");
        environment.ValidUris.Add("https://gutendex.com/books");
        return environment;
    }

    private static MockLibrarySourceEnvironment CreateOfflineEnvironment()
    {
        var environment = new MockLibrarySourceEnvironment();
        environment.ExistingDirectories.Add(MockRootDirectory);
        environment.ExistingFiles[MockInpxFilePath] = 100;
        environment.ExistingFiles[$"{MockRootDirectory}\\a.fb2-000001-000100.zip"] = 200;
        environment.ExistingFiles[$"{MockRootDirectory}\\b.fb2-000101-000200.7z"] = 300;
        environment.ExistingFiles[$"{MockRootDirectory}\\c.fb2-000201-000300.gz"] = 400;
        environment.ExistingFiles[$"{MockRootDirectory}\\ignore.txt"] = 500;
        return environment;
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

    private sealed class MockLibrarySourceEnvironment : ILibrarySourceEnvironment
    {
        public HashSet<string> ValidUris { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsValidAbsoluteUri(string value)
            => ValidUris.Contains(value);

        public bool FileExists(string path)
            => ExistingFiles.ContainsKey(path);

        public long? GetFileSize(string path)
            => ExistingFiles.TryGetValue(path, out var sizeBytes) ? sizeBytes : null;

        public bool DirectoryExists(string path)
            => ExistingDirectories.Contains(path);

        public IReadOnlyList<string> EnumerateFiles(string directoryPath)
            // The production implementation scans a single directory, so the mock
            // mirrors that contract instead of trying to emulate a real file system.
            => ExistingFiles.Keys.Where(path => string.Equals(Path.GetDirectoryName(path), directoryPath, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
