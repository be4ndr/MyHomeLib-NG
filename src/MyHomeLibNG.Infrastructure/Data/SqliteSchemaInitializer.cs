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

        var sql = """
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

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureColumnAsync(connection, "ProviderId", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "ApiBaseUrl", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "SearchEndpoint", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "InpxFilePath", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "ArchiveDirectoryPath", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(LibraryProfiles);";

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
        alterCommand.CommandText = $"ALTER TABLE LibraryProfiles ADD COLUMN {columnName} {columnDefinition};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
