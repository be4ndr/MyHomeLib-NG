using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IFb2MetadataParser
{
    Task<Fb2BookMetadata> ParseAsync(
        Stream stream,
        Fb2ParsingOptions? options = null,
        CancellationToken cancellationToken = default);
}
