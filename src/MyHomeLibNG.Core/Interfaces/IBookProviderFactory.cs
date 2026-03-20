using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IBookProviderFactory
{
    IBookProvider CreateProvider(LibraryProfile profile);
}
