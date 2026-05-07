using Microsoft.Data.Sqlite;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using System.Globalization;
using System.Text;

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

    public async Task<ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
        long libraryProfileId,
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateImportedBookSelectByLocationCommand(connection);
        command.Parameters["$libraryProfileId"].Value = libraryProfileId;
        command.Parameters["$archivePath"].Value = archivePath;
        command.Parameters["$entryPath"].Value = entryPath;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadImportedBook(reader);
    }

    public async Task<long> GetImportedBookCountAsync(long libraryProfileId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT COUNT(*)
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId;
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", libraryProfileId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<ImportedBookMetadataSnapshot>> SearchImportedBooksAsync(
        long libraryProfileId,
        string query,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        var queryTokens = BuildSearchTokens(query);
        var hasQuery = queryTokens.Length > 0;
        var whereClause = hasQuery
            ? string.Join(" AND ", queryTokens.Select((_, index) =>
                $"(Title LIKE $likeQuery{index} ESCAPE '\\' OR Authors LIKE $likeQuery{index} ESCAPE '\\' OR Series LIKE $likeQuery{index} ESCAPE '\\' OR Genres LIKE $likeQuery{index} ESCAPE '\\' OR Annotation LIKE $likeQuery{index} ESCAPE '\\' OR Language LIKE $likeQuery{index} ESCAPE '\\' OR FileName LIKE $likeQuery{index} ESCAPE '\\' OR EntryPath LIKE $likeQuery{index} ESCAPE '\\' OR ArchivePath LIKE $likeQuery{index} ESCAPE '\\')"))
            : "1=1";

        command.CommandText = $"""
                              SELECT Title,
                                     Authors,
                                     Annotation,
                                     PublishYear,
                                     Series,
                                     SeriesNumber,
                                     Genres,
                                     Language,
                                     ArchivePath,
                                     EntryPath,
                                     FileName,
                                     FileSize,
                                     ContentHash,
                                     CoverThumbnail
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                                AND {whereClause}
                              ORDER BY Title COLLATE NOCASE, Authors COLLATE NOCASE, EntryPath COLLATE NOCASE;
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", libraryProfileId);
        for (var index = 0; index < queryTokens.Length; index++)
        {
            command.Parameters.AddWithValue($"$likeQuery{index}", BuildLikePattern(queryTokens[index]));
        }

        var results = new List<ImportedBookMetadataSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadImportedBook(reader));
        }

        return results;
    }

    public async Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default)
    {
        ValidateImportedBook(book);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var upsertCommand = CreateUpsertCommand(connection, transaction);
        ApplyUpsertParameters(upsertCommand, book);
        await upsertCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var selectIdCommand = connection.CreateCommand();
        selectIdCommand.Transaction = transaction;
        selectIdCommand.CommandText = """
                                      SELECT Id
                                      FROM Books
                                      WHERE LibraryProfileId = $libraryProfileId
                                        AND ArchivePath = $archivePath
                                        AND EntryPath = $entryPath;
                                      """;
        selectIdCommand.Parameters.AddWithValue("$libraryProfileId", book.LibraryProfileId);
        selectIdCommand.Parameters.AddWithValue("$archivePath", book.ArchivePath);
        selectIdCommand.Parameters.AddWithValue("$entryPath", book.EntryPath);

        var id = await selectIdCommand.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Convert.ToInt64(id, CultureInfo.InvariantCulture);
    }

    public async Task<BookImportBatchResult> UpsertImportedBooksAsync(
        IReadOnlyList<BookImportRecord> books,
        CancellationToken cancellationToken = default)
    {
        if (books.Count == 0)
        {
            return new BookImportBatchResult();
        }

        foreach (var book in books)
        {
            ValidateImportedBook(book);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var existingHashes = await LoadExistingHashesAsync(connection, transaction, books, cancellationToken);

        await using var upsertCommand = CreateUpsertCommand(connection, transaction);

        var booksAdded = 0;
        var booksUpdated = 0;
        var booksSkipped = 0;

        foreach (var book in books)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = (book.LibraryProfileId, book.ArchivePath, book.EntryPath);
            existingHashes.TryGetValue(key, out var existingHash);

            if (string.Equals(existingHash, book.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                booksSkipped++;
                continue;
            }

            ApplyUpsertParameters(upsertCommand, book);
            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);

            if (existingHash is null)
            {
                booksAdded++;
            }
            else
            {
                booksUpdated++;
            }

            existingHashes[key] = book.ContentHash;
        }

        await transaction.CommitAsync(cancellationToken);
        return new BookImportBatchResult
        {
            BooksAdded = booksAdded,
            BooksUpdated = booksUpdated,
            BooksSkipped = booksSkipped
        };
    }

    private static async Task<Dictionary<(long LibraryProfileId, string ArchivePath, string EntryPath), string?>> LoadExistingHashesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<BookImportRecord> books,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(long LibraryProfileId, string ArchivePath, string EntryPath), string?>();

        var commandText = new StringBuilder(
            """
            WITH Requested(LibraryProfileId, ArchivePath, EntryPath) AS (
                VALUES
            """);

        for (var index = 0; index < books.Count; index++)
        {
            if (index > 0)
            {
                commandText.AppendLine(",");
            }

            commandText.Append($"    ($libraryProfileId{index}, $archivePath{index}, $entryPath{index})");
        }

        commandText.AppendLine();
        commandText.Append(
            """
            )
            SELECT b.LibraryProfileId,
                   b.ArchivePath,
                   b.EntryPath,
                   b.ContentHash
            FROM Books b
            INNER JOIN Requested r
                ON b.LibraryProfileId = r.LibraryProfileId
               AND b.ArchivePath = r.ArchivePath
               AND b.EntryPath = r.EntryPath;
            """);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText.ToString();

        for (var index = 0; index < books.Count; index++)
        {
            var book = books[index];
            command.Parameters.AddWithValue($"$libraryProfileId{index}", book.LibraryProfileId);
            command.Parameters.AddWithValue($"$archivePath{index}", book.ArchivePath);
            command.Parameters.AddWithValue($"$entryPath{index}", book.EntryPath);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = (reader.GetInt64(0), reader.GetString(1), reader.GetString(2));
            var hash = reader.IsDBNull(3) ? null : reader.GetString(3);
            result[key] = hash;
        }

        return result;
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

    private static ImportedBookMetadataSnapshot ReadImportedBook(SqliteDataReader reader)
    {
        return new ImportedBookMetadataSnapshot
        {
            Title = reader.GetString(0),
            Authors = GetNullableString(reader, 1),
            Annotation = GetNullableString(reader, 2),
            PublishYear = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Series = GetNullableString(reader, 4),
            SeriesNumber = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            Genres = GetNullableString(reader, 6),
            Language = GetNullableString(reader, 7),
            ArchivePath = reader.GetString(8),
            EntryPath = reader.GetString(9),
            FileName = GetNullableString(reader, 10),
            FileSize = reader.IsDBNull(11) ? null : reader.GetInt64(11),
            ContentHash = GetNullableString(reader, 12),
            CoverThumbnail = reader.IsDBNull(13) ? null : (byte[])reader[13]
        };
    }

    private static SqliteCommand CreateImportedBookSelectByLocationCommand(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT Title,
                                     Authors,
                                     Annotation,
                                     PublishYear,
                                     Series,
                                     SeriesNumber,
                                     Genres,
                                     Language,
                                     ArchivePath,
                                     EntryPath,
                                     FileName,
                                     FileSize,
                                     ContentHash,
                                     CoverThumbnail
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                                AND ArchivePath = $archivePath
                                AND EntryPath = $entryPath;
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", 0L);
        command.Parameters.AddWithValue("$archivePath", string.Empty);
        command.Parameters.AddWithValue("$entryPath", string.Empty);
        return command;
    }

    private static SqliteCommand CreateUpsertCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
                              INSERT INTO Books(
                                  LibraryProfileId,
                                  Title,
                                  Authors,
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
                                  $authors,
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
                                  Authors = excluded.Authors,
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
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", 0L);
        command.Parameters.AddWithValue("$title", string.Empty);
        command.Parameters.AddWithValue("$authors", DBNull.Value);
        command.Parameters.AddWithValue("$annotation", DBNull.Value);
        command.Parameters.AddWithValue("$publishYear", DBNull.Value);
        command.Parameters.AddWithValue("$primaryFormat", 0);
        command.Parameters.AddWithValue("$series", DBNull.Value);
        command.Parameters.AddWithValue("$seriesNumber", DBNull.Value);
        command.Parameters.AddWithValue("$genres", DBNull.Value);
        command.Parameters.AddWithValue("$language", DBNull.Value);
        command.Parameters.AddWithValue("$archivePath", string.Empty);
        command.Parameters.AddWithValue("$entryPath", string.Empty);
        command.Parameters.AddWithValue("$fileName", DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", DBNull.Value);
        command.Parameters.AddWithValue("$contentHash", DBNull.Value);
        command.Parameters.AddWithValue("$coverThumbnail", DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", string.Empty);
        command.Parameters.AddWithValue("$updatedAt", string.Empty);
        return command;
    }

    private static void ApplyUpsertParameters(SqliteCommand command, BookImportRecord book)
    {
        command.Parameters["$libraryProfileId"].Value = book.LibraryProfileId;
        command.Parameters["$title"].Value = book.Title;
        command.Parameters["$authors"].Value = book.Authors ?? (object)DBNull.Value;
        command.Parameters["$annotation"].Value = book.Annotation ?? (object)DBNull.Value;
        command.Parameters["$publishYear"].Value = book.PublishYear ?? (object)DBNull.Value;
        command.Parameters["$primaryFormat"].Value = (int)book.PrimaryFormat;
        command.Parameters["$series"].Value = book.Series ?? (object)DBNull.Value;
        command.Parameters["$seriesNumber"].Value = book.SeriesNumber ?? (object)DBNull.Value;
        command.Parameters["$genres"].Value = book.Genres ?? (object)DBNull.Value;
        command.Parameters["$language"].Value = book.Language ?? (object)DBNull.Value;
        command.Parameters["$archivePath"].Value = book.ArchivePath;
        command.Parameters["$entryPath"].Value = book.EntryPath;
        command.Parameters["$fileName"].Value = book.FileName ?? (object)DBNull.Value;
        command.Parameters["$fileSize"].Value = book.FileSize ?? (object)DBNull.Value;
        command.Parameters["$contentHash"].Value = book.ContentHash ?? (object)DBNull.Value;
        command.Parameters["$coverThumbnail"].Value = book.CoverThumbnail ?? (object)DBNull.Value;
        command.Parameters["$createdAt"].Value = book.CreatedAt.ToString("O");
        command.Parameters["$updatedAt"].Value = book.UpdatedAt.ToString("O");
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

    private static string BuildLikePattern(string query)
    {
        var escaped = query
            .Trim()
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private static string[] BuildSearchTokens(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            ? []
            : query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
