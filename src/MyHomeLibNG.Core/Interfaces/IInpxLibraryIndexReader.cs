using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

/// <summary>
/// Reads local offline INPX catalog data and maps it into SQLite import records.
/// </summary>
public interface IInpxLibraryIndexReader
{
    /// <summary>
    /// Builds import records from the configured INPX catalog for a folder library profile.
    /// </summary>
    Task<IReadOnlyList<BookImportRecord>> ReadImportRecordsAsync(
        LibraryProfile profile,
        CancellationToken cancellationToken = default);
}
