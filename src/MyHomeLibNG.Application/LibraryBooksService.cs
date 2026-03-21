using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Application;

public sealed class LibraryBooksService
{
    private readonly IBookProviderFactory _providerFactory;
    private readonly IActiveLibraryContext _activeLibraryContext;

    public LibraryBooksService(
        IBookProviderFactory providerFactory,
        IActiveLibraryContext activeLibraryContext)
    {
        _providerFactory = providerFactory;
        _activeLibraryContext = activeLibraryContext;
    }

    public Task<IReadOnlyList<NormalizedBook>> SearchAsync(
        LibraryProfile profile,
        string query,
        CancellationToken cancellationToken = default)
    {
        return SearchNormalizedAsync(profile, query, cancellationToken);
    }

    public Task<NormalizedBook?> GetByIdAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return GetByIdNormalizedAsync(profile, sourceId, cancellationToken);
    }

    public Task<Stream> OpenContentAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return OpenContentCoreAsync(profile, sourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<BookSearchResult>> SearchLibraryAsync(
        string query,
        LibraryProfile? profile = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProfile = await ResolveProfileAsync(profile, cancellationToken);
        var provider = _providerFactory.CreateProvider(resolvedProfile);
        var results = await provider.SearchAsync(query, cancellationToken);

        return results
            .Select(book => BookResultMapper.ToSearchResult(resolvedProfile, provider.Capabilities, book))
            .ToArray();
    }

    public Task<IReadOnlyList<BookSearchResult>> SearchActiveLibraryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return SearchLibraryAsync(query, null, cancellationToken);
    }

    public async Task<BookDetails?> GetBookDetailsAsync(
        string sourceId,
        LibraryProfile? profile = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProfile = await ResolveProfileAsync(profile, cancellationToken);
        var provider = _providerFactory.CreateProvider(resolvedProfile);
        var book = await provider.GetByIdAsync(sourceId, cancellationToken);

        return book is null
            ? null
            : BookResultMapper.ToDetails(resolvedProfile, provider.Capabilities, book);
    }

    public Task<BookDetails?> GetBookByIdFromActiveLibraryAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return GetBookDetailsAsync(sourceId, null, cancellationToken);
    }

    public async Task<Stream> OpenBookContentAsync(
        string sourceId,
        LibraryProfile? profile = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProfile = await ResolveProfileAsync(profile, cancellationToken);
        return await OpenContentCoreAsync(resolvedProfile, sourceId, cancellationToken);
    }

    public Task<Stream> OpenBookContentFromActiveLibraryAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return OpenBookContentAsync(sourceId, null, cancellationToken);
    }

    private async Task<LibraryProfile> ResolveProfileAsync(
        LibraryProfile? explicitProfile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (explicitProfile is not null)
        {
            return explicitProfile;
        }

        var activeProfile = _activeLibraryContext.Current;
        if (activeProfile is not null)
        {
            return activeProfile;
        }

        throw new InvalidOperationException("No active library is selected.");
    }

    private Task<IReadOnlyList<NormalizedBook>> SearchNormalizedAsync(
        LibraryProfile profile,
        string query,
        CancellationToken cancellationToken)
    {
        return _providerFactory.CreateProvider(profile).SearchAsync(query, cancellationToken);
    }

    private Task<NormalizedBook?> GetByIdNormalizedAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken)
    {
        return _providerFactory.CreateProvider(profile).GetByIdAsync(sourceId, cancellationToken);
    }

    private Task<Stream> OpenContentCoreAsync(
        LibraryProfile profile,
        string sourceId,
        CancellationToken cancellationToken)
    {
        return _providerFactory.CreateProvider(profile).OpenContentAsync(sourceId, cancellationToken);
    }
}
