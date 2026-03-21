using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

public sealed class ProjectGutenbergBookProviderRegistration : IBookProviderRegistration
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TransientHttpExecutor _httpExecutor;

    public ProjectGutenbergBookProviderRegistration(
        IHttpClientFactory httpClientFactory,
        TransientHttpExecutor httpExecutor)
    {
        _httpClientFactory = httpClientFactory;
        _httpExecutor = httpExecutor;
    }

    public string ProviderId => BookProviderIds.ProjectGutenberg;

    public bool CanCreate(LibraryProfile profile)
        => string.Equals(profile.ProviderId, ProviderId, StringComparison.OrdinalIgnoreCase);

    public IBookProvider Create(LibraryProfile profile)
        => new ProjectGutenbergBookProvider(
            _httpClientFactory.CreateClient(HttpClientNames.ProjectGutenberg),
            _httpExecutor);
}
