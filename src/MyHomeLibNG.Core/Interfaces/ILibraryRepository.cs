using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface ILibraryRepository
{
    Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default);
    Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default);
    Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default);
    Task<ImportedBookMetadataSnapshot?> GetImportedBookMetadataAsync(
        long libraryProfileId,
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken = default);
    Task<long> GetImportedBookCountAsync(long libraryProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportedBookMetadataSnapshot>> SearchImportedBooksAsync(
        long libraryProfileId,
        string query,
        CancellationToken cancellationToken = default);
    Task<long> UpsertImportedBookAsync(BookImportRecord book, CancellationToken cancellationToken = default);
    Task<BookImportBatchResult> UpsertImportedBooksAsync(
        IReadOnlyList<BookImportRecord> books,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(long libraryId, CancellationToken cancellationToken = default);
}
