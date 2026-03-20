using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class LibraryBooksService
{
    private readonly IBookProviderFactory _providerFactory;

    public LibraryBooksService(IBookProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public Task<IReadOnlyList<NormalizedBook>> SearchAsync(
        LibraryProfile profile,
        string query,
        CancellationToken cancellationToken = default)
    {
        return _providerFactory.CreateProvider(profile).SearchAsync(query, cancellationToken);
    }

    public Task<NormalizedBook?> GetByIdAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return _providerFactory.CreateProvider(profile).GetByIdAsync(sourceId, cancellationToken);
    }

    public Task<Stream> OpenContentAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return _providerFactory.CreateProvider(profile).OpenContentAsync(sourceId, cancellationToken);
    }
}
