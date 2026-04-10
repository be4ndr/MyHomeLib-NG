using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IFb2MetadataParser
{
    Task<Fb2BookMetadata> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
