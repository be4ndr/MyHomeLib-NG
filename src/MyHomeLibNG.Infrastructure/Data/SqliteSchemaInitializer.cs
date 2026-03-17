using Microsoft.Data.Sqlite;

namespace MyHomeLibNext.Infrastructure.Data;

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
                      LibraryType INTEGER NOT NULL,
                      ConnectionInfo TEXT NOT NULL,
                      CreatedAtUtc TEXT NOT NULL,
                      LastOpenedAtUtc TEXT NULL
                  );
                  """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
