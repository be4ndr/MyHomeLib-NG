namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookLocation
{
    public string ContainerPath { get; init; } = string.Empty;
    public string? ArchiveEntryPath { get; init; }
}
