using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers;

public sealed class BookProviderFactory : IBookProviderFactory
{
    private readonly IReadOnlyList<IBookProviderRegistration> _registrations;

    public BookProviderFactory(IEnumerable<IBookProviderRegistration> registrations)
    {
        _registrations = registrations.ToArray();
    }

    public IBookProvider CreateProvider(LibraryProfile profile)
    {
        var registration = _registrations.FirstOrDefault(candidate => candidate.CanCreate(profile));
        if (registration is null)
        {
            throw new InvalidOperationException($"Unknown provider '{profile.ProviderId}'.");
        }

        return registration.Create(profile);
    }
}
