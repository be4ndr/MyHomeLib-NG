using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Online;

internal static class ProfileConfiguredHttpClient
{
    public static HttpClient Create(IHttpClientFactory httpClientFactory, string clientName, LibraryProfile profile)
    {
        var client = httpClientFactory.CreateClient(clientName);
        var configuredBaseUrl = profile.OnlineSource?.ApiBaseUrl;
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseAddress) &&
            (baseAddress.Scheme == Uri.UriSchemeHttp || baseAddress.Scheme == Uri.UriSchemeHttps))
        {
            client.BaseAddress = baseAddress;
        }

        return client;
    }
}
