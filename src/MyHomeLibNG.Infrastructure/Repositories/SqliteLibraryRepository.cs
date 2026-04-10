using Microsoft.Data.Sqlite;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using System.Globalization;

namespace MyHomeLibNG.Infrastructure.Repositories;

public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private static readonly DateTimeStyles TimestampStyles = DateTimeStyles.RoundtripKind;
    private const string SelectProfileColumns = """
                                                SELECT Id, Name, ProviderId, LibraryType, ConnectionInfo, ApiBaseUrl, SearchEndpoint, InpxFilePath, ArchiveDirectoryPath, CreatedAtUtc, LastOpenedAtUtc
                                                FROM LibraryProfiles
                                                """;
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
        command.CommandText = $"{SelectProfileColumns}\nORDER BY Name;";

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
        command.CommandText = $"{SelectProfileColumns}\nWHERE Id = $id;";
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
        ValidateProfile(profile);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // ConnectionInfo is kept as a compact legacy summary while the structured
        // columns carry the real source-specific configuration.
        command.CommandText = """
                              INSERT INTO LibraryProfiles(
                                  Name,
                                  ProviderId,
                                  LibraryType,
                                  ConnectionInfo,
                                  ApiBaseUrl,
                                  SearchEndpoint,
                                  InpxFilePath,
                                  ArchiveDirectoryPath,
                                  CreatedAtUtc,
                                  LastOpenedAtUtc)
                              VALUES (
                                  $name,
                                  $providerId,
                                  $libraryType,
                                  $connectionInfo,
                                  $apiBaseUrl,
                                  $searchEndpoint,
                                  $inpxFilePath,
                                  $archiveDirectoryPath,
                                  $createdAtUtc,
                                  $lastOpenedAtUtc);
                              SELECT last_insert_rowid();
                              """;
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$providerId", profile.ProviderId);
        command.Parameters.AddWithValue("$libraryType", (int)profile.LibraryType);
        command.Parameters.AddWithValue("$connectionInfo", BuildConnectionInfo(profile));
        command.Parameters.AddWithValue("$apiBaseUrl", profile.OnlineSource?.ApiBaseUrl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$searchEndpoint", profile.OnlineSource?.SearchEndpoint ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$inpxFilePath", profile.FolderSource?.InpxFilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$archiveDirectoryPath", profile.FolderSource?.ArchiveDirectoryPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", profile.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$lastOpenedAtUtc", profile.LastOpenedAtUtc?.ToString("O") ?? (object)DBNull.Value);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(id);
    }

    public async Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default)
    {
        ValidateImportedBook(book);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO Books(
                                  LibraryProfileId,
                                  Title,
                                  Annotation,
                                  PublishYear,
                                  PrimaryFormat,
                                  Series,
                                  SeriesNumber,
                                  Genres,
                                  Language,
                                  ArchivePath,
                                  EntryPath,
                                  FileName,
                                  FileSize,
                                  ContentHash,
                                  CoverThumbnail,
                                  CreatedAt,
                                  UpdatedAt)
                              VALUES (
                                  $libraryProfileId,
                                  $title,
                                  $annotation,
                                  $publishYear,
                                  $primaryFormat,
                                  $series,
                                  $seriesNumber,
                                  $genres,
                                  $language,
                                  $archivePath,
                                  $entryPath,
                                  $fileName,
                                  $fileSize,
                                  $contentHash,
                                  $coverThumbnail,
                                  $createdAt,
                                  $updatedAt)
                              ON CONFLICT(LibraryProfileId, ArchivePath, EntryPath) DO UPDATE SET
                                  Title = excluded.Title,
                                  Annotation = excluded.Annotation,
                                  PublishYear = excluded.PublishYear,
                                  PrimaryFormat = excluded.PrimaryFormat,
                                  Series = excluded.Series,
                                  SeriesNumber = excluded.SeriesNumber,
                                  Genres = excluded.Genres,
                                  Language = excluded.Language,
                                  FileName = excluded.FileName,
                                  FileSize = excluded.FileSize,
                                  ContentHash = excluded.ContentHash,
                                  CoverThumbnail = excluded.CoverThumbnail,
                                  UpdatedAt = excluded.UpdatedAt;
                              SELECT Id
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                                AND ArchivePath = $archivePath
                                AND EntryPath = $entryPath;
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", book.LibraryProfileId);
        command.Parameters.AddWithValue("$title", book.Title);
        command.Parameters.AddWithValue("$annotation", book.Annotation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$publishYear", book.PublishYear ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$primaryFormat", (int)book.PrimaryFormat);
        command.Parameters.AddWithValue("$series", book.Series ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$seriesNumber", book.SeriesNumber ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$genres", book.Genres ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$language", book.Language ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$archivePath", book.ArchivePath);
        command.Parameters.AddWithValue("$entryPath", book.EntryPath);
        command.Parameters.AddWithValue("$fileName", book.FileName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", book.FileSize ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$contentHash", book.ContentHash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$coverThumbnail", book.CoverThumbnail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", book.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", book.UpdatedAt.ToString("O"));

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(id);
    }

    public async Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              DELETE FROM LibraryProfiles
                              WHERE Id = $id;
                              """;
        command.Parameters.AddWithValue("$id", libraryId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static LibraryProfile ReadProfile(SqliteDataReader reader)
    {
        var record = new ProfileRecord(
            Id: reader.GetInt64(0),
            Name: reader.GetString(1),
            ProviderId: reader.GetString(2),
            LibraryType: (LibraryType)reader.GetInt32(3),
            ApiBaseUrl: GetNullableString(reader, 5),
            SearchEndpoint: GetNullableString(reader, 6),
            InpxFilePath: GetNullableString(reader, 7),
            ArchiveDirectoryPath: GetNullableString(reader, 8),
            CreatedAtUtc: DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture, TimestampStyles),
            LastOpenedAtUtc: ParseNullableTimestamp(GetNullableString(reader, 10)));

        return new LibraryProfile
        {
            Id = record.Id,
            Name = record.Name,
            ProviderId = record.ProviderId,
            LibraryType = record.LibraryType,
            OnlineSource = BuildOnlineSource(record),
            FolderSource = BuildFolderSource(record),
            CreatedAtUtc = record.CreatedAtUtc,
            LastOpenedAtUtc = record.LastOpenedAtUtc
        };
    }

    private static string BuildConnectionInfo(LibraryProfile profile)
    {
        return profile.LibraryType switch
        {
            LibraryType.Online => profile.OnlineSource?.ApiBaseUrl ?? string.Empty,
            LibraryType.Folder => $"{profile.FolderSource?.InpxFilePath}|{profile.FolderSource?.ArchiveDirectoryPath}",
            _ => string.Empty
        };
    }

    private static void ValidateProfile(LibraryProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Library profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ProviderId))
        {
            throw new InvalidOperationException("Library profile provider ID is required.");
        }

        switch (profile.LibraryType)
        {
            case LibraryType.Online when profile.OnlineSource is null:
                throw new InvalidOperationException("Online library profiles require online source settings.");
            case LibraryType.Online when string.IsNullOrWhiteSpace(profile.OnlineSource.ApiBaseUrl):
                throw new InvalidOperationException("Online library profiles require an API base URL.");
            case LibraryType.Folder when profile.FolderSource is null:
                throw new InvalidOperationException("Folder library profiles require folder source settings.");
            case LibraryType.Folder when string.IsNullOrWhiteSpace(profile.FolderSource.InpxFilePath):
                throw new InvalidOperationException("Folder library profiles require an INPX file path.");
            case LibraryType.Folder when string.IsNullOrWhiteSpace(profile.FolderSource.ArchiveDirectoryPath):
                throw new InvalidOperationException("Folder library profiles require an archive directory path.");
        }
    }

    private static void ValidateImportedBook(BookImportRecord book)
    {
        if (book.LibraryProfileId <= 0)
        {
            throw new InvalidOperationException("Imported books require a valid library profile ID.");
        }

        if (string.IsNullOrWhiteSpace(book.Title))
        {
            throw new InvalidOperationException("Imported books require a title.");
        }

        if (string.IsNullOrWhiteSpace(book.ArchivePath))
        {
            throw new InvalidOperationException("Imported books require an archive path.");
        }

        if (string.IsNullOrWhiteSpace(book.EntryPath))
        {
            throw new InvalidOperationException("Imported books require an archive entry path.");
        }
    }

    private static OnlineLibrarySourceSettings? BuildOnlineSource(ProfileRecord record)
    {
        if (record.LibraryType != LibraryType.Online || string.IsNullOrWhiteSpace(record.ApiBaseUrl))
        {
            return null;
        }

        return new OnlineLibrarySourceSettings
        {
            ApiBaseUrl = record.ApiBaseUrl,
            SearchEndpoint = record.SearchEndpoint
        };
    }

    private static FolderLibrarySourceSettings? BuildFolderSource(ProfileRecord record)
    {
        if (record.LibraryType != LibraryType.Folder ||
            string.IsNullOrWhiteSpace(record.InpxFilePath) ||
            string.IsNullOrWhiteSpace(record.ArchiveDirectoryPath))
        {
            return null;
        }

        return new FolderLibrarySourceSettings
        {
            InpxFilePath = record.InpxFilePath,
            ArchiveDirectoryPath = record.ArchiveDirectoryPath
        };
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTimeOffset? ParseNullableTimestamp(string? value)
        => value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, TimestampStyles);

    private sealed record ProfileRecord(
        long Id,
        string Name,
        string ProviderId,
        LibraryType LibraryType,
        string? ApiBaseUrl,
        string? SearchEndpoint,
        string? InpxFilePath,
        string? ArchiveDirectoryPath,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastOpenedAtUtc);
}
