using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.Infrastructure.Data;

namespace MyHomeLibNG.Infrastructure;

public static class ServiceProviderExtensions
{
    public static Task InitializeMyHomeLibInfrastructureAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        return serviceProvider
            .GetRequiredService<SqliteSchemaInitializer>()
            .InitializeAsync(cancellationToken);
    }
}
