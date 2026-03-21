namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineFileMetadata
{
    public long LengthBytes { get; init; }
    public DateTimeOffset LastWriteTimeUtc { get; init; }
}
