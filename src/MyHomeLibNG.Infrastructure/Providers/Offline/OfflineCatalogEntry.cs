using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineCatalogEntry
{
    public NormalizedBook Book { get; init; } = new();
    public string ContainerPath { get; init; } = string.Empty;
    public string? ArchiveEntryPath { get; init; }
    public int? SeriesNumber { get; init; }
    public long? FileSize { get; init; }
    public string? LibId { get; init; }
}
