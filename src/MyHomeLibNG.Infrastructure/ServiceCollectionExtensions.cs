using Microsoft.Extensions.DependencyInjection;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Providers;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using MyHomeLibNG.Infrastructure.Repositories;
using MyHomeLibNG.Infrastructure.Services;

namespace MyHomeLibNG.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyHomeLibInfrastructure(this IServiceCollection services, string connectionString)
    {
        var initializer = new SqliteSchemaInitializer(connectionString);
        initializer.InitializeAsync().GetAwaiter().GetResult();

        services.AddHttpClient(HttpClientNames.ProjectGutenberg, client =>
        {
            client.BaseAddress = new Uri("https://www.gutenberg.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyHomeLibNG/1.0");
        });
        services.AddHttpClient(HttpClientNames.OpenLibrary, client =>
        {
            client.BaseAddress = new Uri("https://openlibrary.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyHomeLibNG/1.0");
        });
        services.AddHttpClient(HttpClientNames.GoogleBooks, client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyHomeLibNG/1.0");
        });

        services.AddSingleton<ILibraryRepository>(_ => new SqliteLibraryRepository(connectionString));
        services.AddSingleton<TransientHttpExecutor>();
        services.AddSingleton<IInpxCatalogParser, InpxCatalogParser>();
        services.AddSingleton<IOfflineLibraryFileSystem, OfflineLibraryFileSystem>();
        services.AddSingleton<IOfflineBookLocationResolver, OfflineBookLocationResolver>();
        services.AddSingleton<IOfflineContentStorage, FileSystemContentStorage>();
        services.AddSingleton<IOfflineContentStorage, ZipContentStorage>();
        services.AddSingleton<OfflineContentStorageRegistry>();
        services.AddSingleton<IBookProviderFactory, BookProviderFactory>();
        services.AddSingleton<ILibrarySourceEnvironment, LibrarySourceEnvironment>();
        services.AddSingleton<ILibrarySourceResolver, LibrarySourceResolver>();
        services.AddSingleton(initializer);

        return services;
    }
}
