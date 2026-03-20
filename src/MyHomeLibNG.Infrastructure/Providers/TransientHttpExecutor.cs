using System.Net;

namespace MyHomeLibNG.Infrastructure.Providers;

public sealed class TransientHttpExecutor
{
    private const int MaxAttempts = 3;

    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await action(cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt == MaxAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
            }
        }

        throw new HttpRequestException("The HTTP request failed after transient retries.");
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == (HttpStatusCode)429 ||
               (int)statusCode >= 500;
    }
}
