using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public interface IActiveLibraryContext
{
    LibraryProfile? Current { get; }
    bool HasActiveLibrary { get; }

    Task SetActiveAsync(long libraryId, CancellationToken cancellationToken = default);
    Task SetActiveAsync(LibraryProfile profile, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
