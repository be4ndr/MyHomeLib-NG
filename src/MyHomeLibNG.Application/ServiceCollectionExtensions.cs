using Microsoft.Extensions.DependencyInjection;

namespace MyHomeLibNG.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyHomeLibApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IActiveLibraryContext, ActiveLibraryContext>();
        services.AddSingleton<LibraryProfilesService>();
        services.AddSingleton<LibraryBooksService>();
        services.AddSingleton<BookImportService>();
        services.AddSingleton<LocalLibraryScanCoordinator>();
        return services;
    }
}
