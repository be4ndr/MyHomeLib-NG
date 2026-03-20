using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Providers.Offline;

public sealed class OfflineBookLocationResolver : IOfflineBookLocationResolver
{
    private readonly IOfflineLibraryFileSystem _fileSystem;

    public OfflineBookLocationResolver(IOfflineLibraryFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Task<OfflineBookLocation?> ResolveAsync(
        FolderLibrarySourceSettings sourceSettings,
        OfflineCatalogEntry entry,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var containerPath = ResolveContainerPath(sourceSettings.ArchiveDirectoryPath, entry.ContainerPath);
        if (containerPath is null)
        {
            return Task.FromResult<OfflineBookLocation?>(null);
        }

        return Task.FromResult<OfflineBookLocation?>(new OfflineBookLocation
        {
            ContainerPath = containerPath,
            ArchiveEntryPath = entry.ArchiveEntryPath
        });
    }

    private string? ResolveContainerPath(string rootPath, string configuredPath)
    {
        var candidate = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(rootPath, configuredPath);

        if (_fileSystem.FileExists(candidate))
        {
            return candidate;
        }

        var targetName = Path.GetFileName(configuredPath);
        return _fileSystem
            .EnumerateFilesRecursive(rootPath)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), targetName, StringComparison.OrdinalIgnoreCase));
    }
}
