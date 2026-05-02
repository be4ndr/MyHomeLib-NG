using System.IO.Compression;
using System.Text;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class InpxCatalogParser : IInpxCatalogParser
{
    private static readonly string[] LegacyFlibustaColumns =
    [
        "authors",
        "genres",
        "title",
        "series",
        "series_number",
        "source_id",
        "file_size",
        "lib_id",
        "deleted",
        "format",
        "updated_at",
        "language",
        "rating",
        "keywords",
        "identifier"
    ];

    private static readonly string[] DefaultColumns =
    [
        "source_id",
        "title",
        "authors",
        "language",
        "description",
        "subjects",
        "publisher",
        "published_year",
        "isbn10",
        "isbn13",
        "container_path",
        "archive_entry_path"
    ];

    public async Task<IReadOnlyList<OfflineCatalogEntry>> ParseAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var columns = await ReadColumnsAsync(archive, cancellationToken);
            var items = new List<OfflineCatalogEntry>();

            foreach (var archiveEntry in archive.Entries.Where(entry => entry.Name.EndsWith(".inp", StringComparison.OrdinalIgnoreCase)))
            {
                await using var entryStream = archiveEntry.Open();
                using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var inferredContainerPath = Path.ChangeExtension(archiveEntry.Name, ".zip");

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var parsed = ParseLine(line, columns, sourceName, inferredContainerPath);
                    if (parsed is not null)
                    {
                        items.Add(parsed);
                    }
                }
            }

            return items
                .GroupBy(item => item.Book.SourceId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }
        catch (InvalidDataException)
        {
            return Array.Empty<OfflineCatalogEntry>();
        }
    }

    private static async Task<IReadOnlyList<string>> ReadColumnsAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var structureEntry = archive.Entries.FirstOrDefault(entry => string.Equals(entry.Name, "structure.info", StringComparison.OrdinalIgnoreCase));
        if (structureEntry is null)
        {
            return LegacyFlibustaColumns;
        }

        await using var stream = structureEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var contents = await reader.ReadToEndAsync(cancellationToken);

        var columns = contents
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(column => column.ToLowerInvariant())
            .ToArray();

        return columns.Length == 0 ? DefaultColumns : columns;
    }

    private static OfflineCatalogEntry? ParseLine(
        string line,
        IReadOnlyList<string> columns,
        string sourceName,
        string inferredContainerPath)
    {
        var values = line.Split('\u0004');
        if (values.Length == 0)
        {
            return null;
        }

        if (LooksLikeLegacyFlibustaRow(columns, values))
        {
            return ParseLegacyFlibustaLine(values, sourceName, inferredContainerPath);
        }

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < columns.Count && index < values.Length; index++)
        {
            data[columns[index]] = string.IsNullOrWhiteSpace(values[index]) ? null : values[index].Trim();
        }

        if (!data.TryGetValue("source_id", out var sourceId) ||
            !data.TryGetValue("title", out var title) ||
            !data.TryGetValue("container_path", out var containerPath) ||
            string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(containerPath))
        {
            return null;
        }

        data.TryGetValue("archive_entry_path", out var archiveEntryPath);

        return new OfflineCatalogEntry
        {
            Book = new NormalizedBook
            {
                Source = sourceName,
                SourceId = sourceId,
                Title = title,
                Authors = SplitList(data.GetValueOrDefault("authors")),
                Language = NullIfEmpty(data.GetValueOrDefault("language")),
                Description = NullIfEmpty(data.GetValueOrDefault("description")),
                Subjects = SplitList(data.GetValueOrDefault("subjects")),
                Publisher = NullIfEmpty(data.GetValueOrDefault("publisher")),
                PublishedYear = ParseYear(data.GetValueOrDefault("published_year")),
                Isbn10 = NullIfEmpty(data.GetValueOrDefault("isbn10")),
                Isbn13 = NullIfEmpty(data.GetValueOrDefault("isbn13")),
                Formats = BuildFormats(containerPath, archiveEntryPath)
            },
            ContainerPath = containerPath,
            ArchiveEntryPath = NullIfEmpty(archiveEntryPath)
        };
    }

    private static bool LooksLikeLegacyFlibustaRow(IReadOnlyList<string> columns, IReadOnlyList<string> values)
    {
        return ReferenceEquals(columns, LegacyFlibustaColumns) ||
               (values.Count >= 10 &&
                values[0].Contains(',') &&
                string.Equals(values[9], "fb2", StringComparison.OrdinalIgnoreCase));
    }

    private static OfflineCatalogEntry? ParseLegacyFlibustaLine(
        IReadOnlyList<string> values,
        string sourceName,
        string inferredContainerPath)
    {
        if (values.Count < 10)
        {
            return null;
        }

        var sourceId = NullIfEmpty(values[5]);
        var title = NullIfEmpty(values[2]);
        var format = NullIfEmpty(values[9]) ?? "fb2";
        var archiveEntryPath = sourceId is null ? null : $"{sourceId}.{format}";
        if (sourceId is null || title is null || archiveEntryPath is null)
        {
            return null;
        }

        return new OfflineCatalogEntry
        {
            Book = new NormalizedBook
            {
                Source = sourceName,
                SourceId = sourceId,
                Title = title,
                Authors = ParseLegacyAuthors(values[0]),
                Series = NullIfEmpty(values.ElementAtOrDefault(3)),
                Language = NullIfEmpty(values.ElementAtOrDefault(11)),
                Description = null,
                Subjects = SplitLegacyValues(values[1], ':'),
                Publisher = null,
                PublishedYear = null,
                Formats = [format.ToLowerInvariant()]
            },
            ContainerPath = inferredContainerPath,
            ArchiveEntryPath = archiveEntryPath
        };
    }

    private static IReadOnlyList<string> SplitList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
    }

    private static IReadOnlyList<string> ParseLegacyAuthors(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Replace(',', ' '))
            .Select(NormalizeAuthorWhitespace)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> SplitLegacyValues(string? value, char separator)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim().Trim(':'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeAuthorWhitespace(string value)
    {
        return string.Join(" ", value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string> BuildFormats(string containerPath, string? archiveEntryPath)
    {
        var source = string.IsNullOrWhiteSpace(archiveEntryPath) ? containerPath : archiveEntryPath;
        var extension = Path.GetExtension(source);
        return string.IsNullOrWhiteSpace(extension) ? Array.Empty<string>() : [extension.TrimStart('.').ToLowerInvariant()];
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? ParseYear(string? value)
        => int.TryParse(value, out var year) ? year : null;
}
