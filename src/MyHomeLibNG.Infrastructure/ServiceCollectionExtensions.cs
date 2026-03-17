using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Repositories;

namespace MyHomeLibNG.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyHomeLibInfrastructure(this IServiceCollection services, string connectionString)
    {
        var initializer = new SqliteSchemaInitializer(connectionString);
        initializer.InitializeAsync().GetAwaiter().GetResult();

        services.AddSingleton<ILibraryRepository>(_ => new SqliteLibraryRepository(connectionString));
        services.AddSingleton(initializer);

        return services;
    }
}
