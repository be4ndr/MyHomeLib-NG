using Microsoft.Extensions.DependencyInjection;

namespace MyHomeLibNG.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyHomeLibApplication(this IServiceCollection services)
    {
        services.AddSingleton<LibraryProfilesService>();
        services.AddSingleton<LibraryBooksService>();
        return services;
    }
}
