using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface ILibraryRepository
{
    Task<IReadOnlyList<LibraryProfile>> GetLibraryProfilesAsync(CancellationToken cancellationToken = default);
    Task<LibraryProfile?> GetByIdAsync(long libraryId, CancellationToken cancellationToken = default);
    Task<long> AddAsync(LibraryProfile profile, CancellationToken cancellationToken = default);
}
