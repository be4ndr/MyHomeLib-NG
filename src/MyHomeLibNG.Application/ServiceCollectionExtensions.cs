using Microsoft.Extensions.DependencyInjection;

namespace MyHomeLibNext.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyHomeLibApplication(this IServiceCollection services)
    {
        services.AddSingleton<LibraryProfilesService>();
        return services;
    }
}
