using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNext.Core.Interfaces;
using MyHomeLibNext.Infrastructure.Data;
using MyHomeLibNext.Infrastructure.Repositories;

namespace MyHomeLibNext.Infrastructure;

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
