using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class LibraryProfilesService
{
    private readonly ILibraryRepository _libraryRepository;

    public LibraryProfilesService(ILibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository;
    }

    public Task<IReadOnlyList<LibraryProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        => _libraryRepository.GetLibraryProfilesAsync(cancellationToken);
}
