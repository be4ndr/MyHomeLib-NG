using Microsoft.Data.Sqlite;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Repositories;

public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private readonly string _connectionString;

    public SqliteLibraryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<LibraryProfile>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT Id, Name, LibraryType, ConnectionInfo, CreatedAtUtc, LastOpenedAtUtc
                              FROM LibraryProfiles
                              ORDER BY Name;
                              """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadProfile(reader));
        }

        return result;
    }

    public async Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT Id, Name, LibraryType, ConnectionInfo, CreatedAtUtc, LastOpenedAtUtc
                              FROM LibraryProfiles
                              WHERE Id = $id;
                              """;
        command.Parameters.AddWithValue("$id", libraryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadProfile(reader);
    }

    public async Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO LibraryProfiles(Name, LibraryType, ConnectionInfo, CreatedAtUtc, LastOpenedAtUtc)
                              VALUES ($name, $libraryType, $connectionInfo, $createdAtUtc, $lastOpenedAtUtc);
                              SELECT last_insert_rowid();
                              """;
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$libraryType", (int)profile.LibraryType);
        command.Parameters.AddWithValue("$connectionInfo", profile.ConnectionInfo);
        command.Parameters.AddWithValue("$createdAtUtc", profile.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$lastOpenedAtUtc", profile.LastOpenedAtUtc?.ToString("O") ?? (object)DBNull.Value);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(id);
    }

    private static LibraryProfile ReadProfile(SqliteDataReader reader)
    {
        var lastOpenedRaw = reader.IsDBNull(5) ? null : reader.GetString(5);

        return new LibraryProfile
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            LibraryType = (LibraryType)reader.GetInt32(2),
            ConnectionInfo = reader.GetString(3),
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(4)),
            LastOpenedAtUtc = lastOpenedRaw is null ? null : DateTimeOffset.Parse(lastOpenedRaw)
        };
    }
}
