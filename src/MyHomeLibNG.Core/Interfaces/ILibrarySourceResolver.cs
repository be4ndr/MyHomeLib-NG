using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface ILibrarySourceResolver
{
    Task<LibraryStructure> ResolveAsync(LibraryProfile profile, CancellationToken cancellationToken = default);
}
