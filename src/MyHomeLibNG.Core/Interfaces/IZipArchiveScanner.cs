using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Core.Interfaces;

public interface IZipArchiveScanner
{
    IAsyncEnumerable<ZipArchiveBookEntry> ScanAsync(string path, CancellationToken cancellationToken = default);
}
