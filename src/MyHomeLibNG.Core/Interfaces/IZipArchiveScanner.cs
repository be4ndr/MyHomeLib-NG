using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IZipArchiveScanner
{
    /// <summary>
    /// Resolves ZIP archive file paths from a single ZIP file path or a root directory.
    /// </summary>
    IReadOnlyList<string> ResolveArchivePaths(string path);

    /// <summary>
    /// Opens an archive once and invokes <paramref name="onEntry"/> for each FB2 entry in that archive.
    /// </summary>
    Task ScanArchiveAsync(
        string archivePath,
        Func<ZipArchiveBookEntry, CancellationToken, ValueTask> onEntry,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ZipArchiveBookEntry> ScanAsync(string path, CancellationToken cancellationToken = default);
}
