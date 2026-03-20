using System.Net;
using MyHomeLibNG.Infrastructure.Providers;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class TransientHttpExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientFailures()
    {
        var executor = new TransientHttpExecutor();
        var attempts = 0;

        using var response = await executor.ExecuteAsync(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(attempts < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, attempts);
    }
}
