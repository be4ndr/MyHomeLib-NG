using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers;

public interface IBookProviderRegistration
{
    string ProviderId { get; }

    bool CanCreate(LibraryProfile profile);
    IBookProvider Create(LibraryProfile profile);
}
