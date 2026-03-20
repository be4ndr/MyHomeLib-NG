using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IBookProvider
{
    string Id { get; }
    string DisplayName { get; }
    BookProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<NormalizedBook>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<NormalizedBook?> GetByIdAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<Stream> OpenContentAsync(string sourceId, CancellationToken cancellationToken = default);
}
