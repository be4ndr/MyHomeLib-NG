using Microsoft.Data.Sqlite;

namespace MyHomeLibNG.Infrastructure.Data;

public sealed class SqliteSchemaInitializer
{
    private readonly string _connectionString;

    public SqliteSchemaInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureLibraryProfilesTableAsync(connection, cancellationToken);
        await EnsureBooksTableAsync(connection, cancellationToken);
    }

    private static async Task EnsureLibraryProfilesTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS LibraryProfiles (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               Name TEXT NOT NULL,
                               ProviderId TEXT NOT NULL DEFAULT '',
                               LibraryType INTEGER NOT NULL,
                               ConnectionInfo TEXT NOT NULL,
                               ApiBaseUrl TEXT NULL,
                               SearchEndpoint TEXT NULL,
                               InpxFilePath TEXT NULL,
                               ArchiveDirectoryPath TEXT NULL,
                               CreatedAtUtc TEXT NOT NULL,
                               LastOpenedAtUtc TEXT NULL
                           );
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);

        await EnsureColumnAsync(connection, "LibraryProfiles", "ProviderId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "LibraryProfiles", "ApiBaseUrl", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "LibraryProfiles", "SearchEndpoint", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "LibraryProfiles", "InpxFilePath", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "LibraryProfiles", "ArchiveDirectoryPath", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureBooksTableAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS Books (
                               Id INTEGER PRIMARY KEY AUTOINCREMENT,
                               LibraryProfileId INTEGER NOT NULL,
                               Title TEXT NOT NULL,
                               Annotation TEXT NULL,
                               PublishYear INTEGER NULL,
                               PrimaryFormat INTEGER NOT NULL,
                               Series TEXT NULL,
                               SeriesNumber INTEGER NULL,
                               Genres TEXT NULL,
                               Language TEXT NULL,
                               ArchivePath TEXT NOT NULL,
                               EntryPath TEXT NOT NULL,
                               FileName TEXT NULL,
                               FileSize INTEGER NULL,
                               ContentHash TEXT NULL,
                               CoverThumbnail BLOB NULL,
                               CreatedAt TEXT NOT NULL,
                               UpdatedAt TEXT NOT NULL
                           );
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);

        await EnsureColumnAsync(connection, "Books", "LibraryProfileId", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "Title", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "Annotation", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "PublishYear", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "PrimaryFormat", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "Series", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "SeriesNumber", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "Genres", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "Language", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "ArchivePath", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "EntryPath", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "FileName", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "FileSize", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "ContentHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "CoverThumbnail", "BLOB NULL", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "CreatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00'", cancellationToken);
        await EnsureColumnAsync(connection, "Books", "UpdatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00'", cancellationToken);

        const string uniqueIndexSql = """
                                      CREATE UNIQUE INDEX IF NOT EXISTS IX_Books_LibraryProfileId_ArchivePath_EntryPath
                                      ON Books (LibraryProfileId, ArchivePath, EntryPath);
                                      """;
        await ExecuteNonQueryAsync(connection, uniqueIndexSql, cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await pragmaCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.DisposeAsync();

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
