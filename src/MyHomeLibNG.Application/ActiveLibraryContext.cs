using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class ActiveLibraryContext : IActiveLibraryContext
{
    private readonly ILibraryRepository _libraryRepository;
    private readonly object _sync = new();
    private LibraryProfile? _current;

    public ActiveLibraryContext(ILibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository;
    }

    public LibraryProfile? Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public bool HasActiveLibrary
    {
        get
        {
            lock (_sync)
            {
                return _current is not null;
            }
        }
    }

    public async Task SetActiveAsync(long libraryId, CancellationToken cancellationToken = default)
    {
        var profile = await _libraryRepository.GetByIdAsync(libraryId, cancellationToken);
        if (profile is null)
        {
            throw new InvalidOperationException($"Library profile '{libraryId}' was not found.");
        }

        SetCurrent(profile);
    }

    public Task SetActiveAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(profile);

        SetCurrent(profile);
        return Task.CompletedTask;
    }

    private void SetCurrent(LibraryProfile profile)
    {
        lock (_sync)
        {
            _current = profile;
        }
    }
}
