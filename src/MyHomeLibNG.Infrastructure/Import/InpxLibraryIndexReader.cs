using System.Security.Cryptography;
using System.Text;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers.Offline;

namespace MyHomeLibNG.Infrastructure.Import;

/// <summary>
/// Builds SQLite import records from an offline INPX catalog without opening archive entries.
/// </summary>
public sealed class InpxLibraryIndexReader : IInpxLibraryIndexReader
{
    private readonly IInpxCatalogParser _catalogParser;
    private readonly IOfflineLibraryFileSystem _fileSystem;

    public InpxLibraryIndexReader(
        IInpxCatalogParser catalogParser,
        IOfflineLibraryFileSystem fileSystem)
    {
        _catalogParser = catalogParser;
        _fileSystem = fileSystem;
    }

    public async Task<IReadOnlyList<BookImportRecord>> ReadImportRecordsAsync(
        LibraryProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Id <= 0)
        {
            throw new InvalidOperationException("INPX index import requires a persisted library profile.");
        }

        var sourceSettings = profile.FolderSource
            ?? throw new InvalidOperationException("INPX index import requires folder source settings.");
        if (string.IsNullOrWhiteSpace(sourceSettings.InpxFilePath))
        {
            throw new InvalidOperationException("INPX index import requires an INPX file path.");
        }

        await using var inpxStream = await _fileSystem.OpenReadAsync(sourceSettings.InpxFilePath, cancellationToken);
        var catalog = await _catalogParser.ParseAsync(inpxStream, profile.Name, cancellationToken);
        if (catalog.Count == 0)
        {
            return Array.Empty<BookImportRecord>();
        }

        var timestamp = DateTimeOffset.UtcNow;
        var records = new List<BookImportRecord>(catalog.Count);

        foreach (var entry in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var archivePath = ResolveArchivePath(sourceSettings.ArchiveDirectoryPath, entry.ContainerPath);
            var entryPath = NullIfWhiteSpace(entry.ArchiveEntryPath);
            if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(entryPath))
            {
                continue;
            }

            var title = NullIfWhiteSpace(entry.Book.Title) ?? entry.Book.SourceId;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var record = new BookImportRecord
            {
                LibraryProfileId = profile.Id,
                Title = title,
                Authors = JoinValues(entry.Book.Authors),
                Annotation = NullIfWhiteSpace(entry.Book.Description),
                PublishYear = entry.Book.PublishedYear,
                PrimaryFormat = ResolvePrimaryFormat(entry.Book.Formats, entryPath),
                Series = NullIfWhiteSpace(entry.Book.Series),
                SeriesNumber = entry.SeriesNumber,
                Genres = JoinValues(entry.Book.Subjects),
                Language = NullIfWhiteSpace(entry.Book.Language),
                ArchivePath = archivePath,
                EntryPath = entryPath,
                FileName = Path.GetFileName(entryPath),
                FileSize = entry.FileSize,
                LibId = NullIfWhiteSpace(entry.LibId),
                ContentHash = BuildContentHash(entry, archivePath, entryPath),
                CoverThumbnail = null,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            };

            records.Add(record);
        }

        return records;
    }

    private static string ResolveArchivePath(string rootPath, string containerPath)
    {
        if (string.IsNullOrWhiteSpace(containerPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(containerPath))
        {
            return containerPath;
        }

        return Path.Combine(rootPath, containerPath);
    }

    private static FileFormat ResolvePrimaryFormat(IReadOnlyList<string> formats, string entryPath)
    {
        var format = formats.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(format))
        {
            format = Path.GetExtension(entryPath).TrimStart('.');
        }

        return format?.ToLowerInvariant() switch
        {
            "fb2" => FileFormat.Fb2,
            "epub" => FileFormat.Epub,
            "pdf" => FileFormat.Pdf,
            "mobi" => FileFormat.Mobi,
            "djvu" => FileFormat.Djvu,
            _ => FileFormat.Unknown
        };
    }

    private static string BuildContentHash(OfflineCatalogEntry entry, string archivePath, string entryPath)
    {
        var payload = string.Join(
            '\u001f',
            entry.Book.SourceId,
            entry.Book.Title,
            JoinValues(entry.Book.Authors) ?? string.Empty,
            entry.Book.Series ?? string.Empty,
            entry.SeriesNumber?.ToString() ?? string.Empty,
            JoinValues(entry.Book.Subjects) ?? string.Empty,
            entry.Book.Language ?? string.Empty,
            entry.Book.PublishedYear?.ToString() ?? string.Empty,
            entry.LibId ?? string.Empty,
            entry.FileSize?.ToString() ?? string.Empty,
            archivePath,
            entryPath);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? JoinValues(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        HashSet<string>? unique = null;
        List<string>? ordered = null;

        foreach (var value in values)
        {
            var normalized = NullIfWhiteSpace(value);
            if (normalized is null)
            {
                continue;
            }

            unique ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!unique.Add(normalized))
            {
                continue;
            }

            ordered ??= new List<string>();
            ordered.Add(normalized);
        }

        return ordered is null || ordered.Count == 0
            ? null
            : string.Join("; ", ordered);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
