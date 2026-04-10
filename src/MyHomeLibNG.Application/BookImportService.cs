using System.Buffers;
using System.Security.Cryptography;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class BookImportService
{
    private readonly IZipArchiveScanner _zipArchiveScanner;
    private readonly IFb2MetadataParser _fb2MetadataParser;
    private readonly ILibraryRepository _libraryRepository;

    public BookImportService(
        IZipArchiveScanner zipArchiveScanner,
        IFb2MetadataParser fb2MetadataParser,
        ILibraryRepository libraryRepository)
    {
        _zipArchiveScanner = zipArchiveScanner;
        _fb2MetadataParser = fb2MetadataParser;
        _libraryRepository = libraryRepository;
    }

    public async Task<BookImportSummary> ImportLibraryAsync(
        LibraryProfile profile,
        string? inputPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Id <= 0)
        {
            throw new InvalidOperationException("Imported books require a persisted library profile.");
        }

        var scanPath = ResolveScanPath(profile, inputPath);
        var failures = new List<BookImportFailure>();
        var archivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entriesDiscovered = 0;
        var importedCount = 0;

        await foreach (var entry in _zipArchiveScanner.ScanAsync(scanPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            archivePaths.Add(entry.ArchivePath);
            entriesDiscovered++;

            try
            {
                var record = await BuildImportRecordAsync(profile.Id, entry, cancellationToken);
                await _libraryRepository.UpsertImportedBookAsync(record, cancellationToken);
                importedCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(new BookImportFailure
                {
                    ArchivePath = entry.ArchivePath,
                    EntryPath = entry.EntryPath,
                    Message = exception.Message
                });
            }
        }

        return new BookImportSummary
        {
            ScanPath = scanPath,
            ArchivesScanned = archivePaths.Count,
            EntriesDiscovered = entriesDiscovered,
            ImportedCount = importedCount,
            Failures = failures
        };
    }

    private async Task<BookImportRecord> BuildImportRecordAsync(
        long libraryProfileId,
        ZipArchiveBookEntry entry,
        CancellationToken cancellationToken)
    {
        await using var stream = await entry.OpenReadAsync(cancellationToken);
        await using var buffer = new MemoryStream(entry.FileSize > 0 && entry.FileSize <= int.MaxValue
            ? (int)entry.FileSize
            : 0);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            while (true)
            {
                var bytesRead = await stream.ReadAsync(rentedBuffer.AsMemory(0, rentedBuffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                hash.AppendData(rentedBuffer, 0, bytesRead);
                await buffer.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }

        buffer.Position = 0;
        var metadata = await _fb2MetadataParser.ParseAsync(buffer, cancellationToken);
        var timestamp = DateTimeOffset.UtcNow;

        return new BookImportRecord
        {
            LibraryProfileId = libraryProfileId,
            Title = string.IsNullOrWhiteSpace(metadata.Title) ? entry.FileName : metadata.Title.Trim(),
            Authors = JoinValues(metadata.Authors),
            Annotation = NullIfWhiteSpace(metadata.Annotation),
            PublishYear = metadata.PublishYear,
            PrimaryFormat = FileFormat.Fb2,
            Series = NullIfWhiteSpace(metadata.Series),
            SeriesNumber = metadata.SeriesNumber,
            Genres = JoinValues(metadata.Genres),
            Language = NullIfWhiteSpace(metadata.Language),
            ArchivePath = entry.ArchivePath,
            EntryPath = entry.EntryPath,
            FileName = entry.FileName,
            FileSize = entry.FileSize,
            ContentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            CoverThumbnail = metadata.ThumbnailBytes,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static string ResolveScanPath(LibraryProfile profile, string? inputPath)
    {
        if (!string.IsNullOrWhiteSpace(inputPath))
        {
            return inputPath.Trim();
        }

        var archiveDirectoryPath = profile.FolderSource?.ArchiveDirectoryPath;
        if (!string.IsNullOrWhiteSpace(archiveDirectoryPath))
        {
            return archiveDirectoryPath;
        }

        throw new InvalidOperationException("Folder library profiles require an archive directory path to import books.");
    }

    private static string? JoinValues(IReadOnlyList<string> values)
    {
        var normalized = values
            .Select(NullIfWhiteSpace)
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join("; ", normalized);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
