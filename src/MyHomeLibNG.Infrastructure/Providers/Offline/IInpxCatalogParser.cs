namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public interface IInpxCatalogParser
{
    Task<IReadOnlyList<OfflineCatalogEntry>> ParseAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default);
}
