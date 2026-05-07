using Microsoft.Data.Sqlite;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Data;
using System.Globalization;
using System.Text;

namespace MyHomeLibNG.Infrastructure.Repositories;

public sealed class SqliteLibraryRepository : ILibraryRepository
{
    private static readonly DateTimeStyles TimestampStyles = DateTimeStyles.RoundtripKind;
    private const int SearchTextBackfillBatchSize = 1000;
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
        var queryTokens = BuildSearchTokens(query);
        if (queryTokens.Length == 0)
        {
            return await SearchAllImportedBooksAsync(connection, libraryProfileId, cancellationToken);
        }

        await BackfillSearchTextBatchAsync(connection, libraryProfileId, SearchTextBackfillBatchSize, cancellationToken);

        var normalizedMatches = await SearchNormalizedRowsAsync(connection, libraryProfileId, queryTokens, cancellationToken);
        var legacyMatches = await SearchLegacyRowsAsync(connection, libraryProfileId, queryTokens, cancellationToken);

        if (legacyMatches.Count == 0)
        {
            return normalizedMatches;
        }

        var combined = new List<ImportedBookMetadataSnapshot>(normalizedMatches.Count + legacyMatches.Count);
        combined.AddRange(normalizedMatches);
        combined.AddRange(legacyMatches);
        combined.Sort(ImportedBookSearchOrderComparer.Instance);
        return combined;
    }

    /// <summary>
    /// Backfills missing <c>SearchText</c> values in bounded batches.
    /// </summary>
    /// <param name="libraryProfileId">Optional library profile scope. When null, all profiles are processed.</param>
    /// <param name="batchSize">Maximum rows to process per batch.</param>
    /// <param name="maxBatches">Maximum number of batches to process in this call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total rows updated.</returns>
    public async Task<int> BackfillSearchTextAsync(
        long? libraryProfileId = null,
        int batchSize = SearchTextBackfillBatchSize,
        int maxBatches = 1,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
        }

        if (maxBatches <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatches), maxBatches, "Max batches must be greater than zero.");
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var totalUpdated = 0;
        for (var batch = 0; batch < maxBatches; batch++)
        {
            var updated = await BackfillSearchTextBatchAsync(connection, libraryProfileId, batchSize, cancellationToken);
            totalUpdated += updated;
            if (updated < batchSize)
            {
                break;
            }
        }

        return totalUpdated;
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
            var hasExisting = existingHashes.TryGetValue(key, out var existingHash);

            if (hasExisting && string.Equals(existingHash, book.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                booksSkipped++;
                continue;
            }

            ApplyUpsertParameters(upsertCommand, book);
            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);

            if (!hasExisting)
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

    private static async Task<int> BackfillSearchTextBatchAsync(
        SqliteConnection connection,
        long? libraryProfileId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = libraryProfileId.HasValue
            ? """
              SELECT Id,
                     Title,
                     Authors,
                     Series,
                     Genres,
                     Annotation,
                     Language,
                     FileName
              FROM Books
              WHERE LibraryProfileId = $libraryProfileId
                AND (SearchText IS NULL OR SearchText = '')
              ORDER BY Id
              LIMIT $batchSize;
              """
            : """
              SELECT Id,
                     Title,
                     Authors,
                     Series,
                     Genres,
                     Annotation,
                     Language,
                     FileName
              FROM Books
              WHERE SearchText IS NULL OR SearchText = ''
              ORDER BY Id
              LIMIT $batchSize;
              """;
        selectCommand.Parameters.AddWithValue("$batchSize", batchSize);
        if (libraryProfileId.HasValue)
        {
            selectCommand.Parameters.AddWithValue("$libraryProfileId", libraryProfileId.Value);
        }

        var updates = new List<(long Id, string SearchText)>(batchSize);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            updates.Add((
                reader.GetInt64(0),
                BookSearchTextNormalizer.BuildSearchText(
                    reader.GetString(1),
                    GetNullableString(reader, 2),
                    GetNullableString(reader, 3),
                    GetNullableString(reader, 4),
                    GetNullableString(reader, 5),
                    GetNullableString(reader, 6),
                    GetNullableString(reader, 7))));
        }

        if (updates.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = """
                                    UPDATE Books
                                    SET SearchText = $searchText
                                    WHERE Id = $id
                                      AND (SearchText IS NULL OR SearchText = '');
                                    """;
        updateCommand.Parameters.AddWithValue("$searchText", string.Empty);
        updateCommand.Parameters.AddWithValue("$id", 0L);

        var updatedCount = 0;
        foreach (var update in updates)
        {
            updateCommand.Parameters["$searchText"].Value = update.SearchText;
            updateCommand.Parameters["$id"].Value = update.Id;
            updatedCount += await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return updatedCount;
    }

    private static async Task<IReadOnlyList<ImportedBookMetadataSnapshot>> SearchAllImportedBooksAsync(
        SqliteConnection connection,
        long libraryProfileId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
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
                                     LibId,
                                     ContentHash,
                                     CoverThumbnail
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                              ORDER BY Title COLLATE NOCASE, Authors COLLATE NOCASE, EntryPath COLLATE NOCASE;
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", libraryProfileId);

        var results = new List<ImportedBookMetadataSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadImportedBook(reader));
        }

        return results;
    }

    private static async Task<List<ImportedBookMetadataSnapshot>> SearchNormalizedRowsAsync(
        SqliteConnection connection,
        long libraryProfileId,
        string[] queryTokens,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var whereClause = string.Join(" AND ", queryTokens.Select((_, index) =>
            $"SearchText LIKE $likeQuery{index} ESCAPE '\\'"));

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
                                     LibId,
                                     ContentHash,
                                     CoverThumbnail
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                                AND SearchText IS NOT NULL
                                AND SearchText <> ''
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

    private static async Task<List<ImportedBookMetadataSnapshot>> SearchLegacyRowsAsync(
        SqliteConnection connection,
        long libraryProfileId,
        string[] queryTokens,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
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
                                     LibId,
                                     ContentHash
                              FROM Books
                              WHERE LibraryProfileId = $libraryProfileId
                                AND (SearchText IS NULL OR SearchText = '');
                              """;
        command.Parameters.AddWithValue("$libraryProfileId", libraryProfileId);

        var results = new List<ImportedBookMetadataSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var book = ReadImportedBookWithoutThumbnail(reader);
            var searchText = BookSearchTextNormalizer.BuildSearchText(
                book.Title,
                book.Authors,
                book.Series,
                book.Genres,
                book.Annotation,
                book.Language,
                book.FileName);
            if (queryTokens.All(token => searchText.Contains(token, StringComparison.Ordinal)))
            {
                results.Add(book);
            }
        }

        results.Sort(ImportedBookSearchOrderComparer.Instance);
        return results;
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
            LibId = GetNullableString(reader, 12),
            ContentHash = GetNullableString(reader, 13),
            CoverThumbnail = reader.IsDBNull(14) ? null : (byte[])reader[14]
        };
    }

    private static ImportedBookMetadataSnapshot ReadImportedBookWithoutThumbnail(SqliteDataReader reader)
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
            LibId = GetNullableString(reader, 12),
            ContentHash = GetNullableString(reader, 13),
            CoverThumbnail = null
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
                                     LibId,
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
                                  LibId,
                                  ContentHash,
                                  SearchText,
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
                                  $libId,
                                  $contentHash,
                                  $searchText,
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
                                  LibId = excluded.LibId,
                                  ContentHash = excluded.ContentHash,
                                  SearchText = excluded.SearchText,
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
        command.Parameters.AddWithValue("$libId", DBNull.Value);
        command.Parameters.AddWithValue("$contentHash", DBNull.Value);
        command.Parameters.AddWithValue("$searchText", string.Empty);
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
        command.Parameters["$libId"].Value = book.LibId ?? (object)DBNull.Value;
        command.Parameters["$contentHash"].Value = book.ContentHash ?? (object)DBNull.Value;
        command.Parameters["$searchText"].Value = BookSearchTextNormalizer.BuildSearchText(
            book.Title,
            book.Authors,
            book.Series,
            book.Genres,
            book.Annotation,
            book.Language,
            book.FileName);
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
        var normalizedQuery = BookSearchTextNormalizer.NormalizeForSearch(query);
        return string.IsNullOrWhiteSpace(normalizedQuery)
            ? []
            : normalizedQuery
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
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

    private sealed class ImportedBookSearchOrderComparer : IComparer<ImportedBookMetadataSnapshot>
    {
        public static readonly ImportedBookSearchOrderComparer Instance = new();

        public int Compare(ImportedBookMetadataSnapshot? x, ImportedBookMetadataSnapshot? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var byTitle = string.Compare(x.Title, y.Title, StringComparison.OrdinalIgnoreCase);
            if (byTitle != 0)
            {
                return byTitle;
            }

            var byAuthors = string.Compare(x.Authors ?? string.Empty, y.Authors ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (byAuthors != 0)
            {
                return byAuthors;
            }

            return string.Compare(x.EntryPath, y.EntryPath, StringComparison.OrdinalIgnoreCase);
        }
    }

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
