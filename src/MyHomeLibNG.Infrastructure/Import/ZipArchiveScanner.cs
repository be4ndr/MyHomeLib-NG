using System.IO.Compression;
using System.Runtime.CompilerServices;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Providers.Offline;

namespace MyHomeLibNG.Infrastructure.Import;

public sealed class ZipArchiveScanner : IZipArchiveScanner
{
    private readonly IOfflineLibraryFileSystem _fileSystem;

    public ZipArchiveScanner(IOfflineLibraryFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async IAsyncEnumerable<ZipArchiveBookEntry> ScanAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        foreach (var archivePath in ResolveArchivePaths(path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await foreach (var entry in ScanArchiveEntriesReopeningAsync(archivePath, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Opens one ZIP archive and executes <paramref name="onEntry"/> for each FB2 entry.
    /// </summary>
    public async Task ScanArchiveAsync(
        string archivePath,
        Func<ZipArchiveBookEntry, CancellationToken, ValueTask> onEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(onEntry);

        await using var archiveStream = await _fileSystem.OpenReadAsync(archivePath, cancellationToken);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return;
        }

        using (archive)
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsFb2Entry(entry))
                {
                    continue;
                }

                var archiveEntry = entry;
                var scanEntry = new ZipArchiveBookEntry
                {
                    ArchivePath = archivePath,
                    EntryPath = archiveEntry.FullName,
                    FileName = archiveEntry.Name,
                    FileSize = archiveEntry.Length,
                    OpenReadAsync = token =>
                    {
                        token.ThrowIfCancellationRequested();
                        return Task.FromResult<Stream>(archiveEntry.Open());
                    }
                };

                await onEntry(scanEntry, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Resolves ZIP archive paths from a ZIP file path or a root folder path.
    /// </summary>
    public IReadOnlyList<string> ResolveArchivePaths(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (_fileSystem.FileExists(path))
        {
            if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>();
            }

            return [path];
        }

        return _fileSystem
            .EnumerateFilesRecursive(path)
            .Where(candidate => candidate.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private async IAsyncEnumerable<ZipArchiveBookEntry> ScanArchiveEntriesReopeningAsync(
        string archivePath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var archiveStream = await _fileSystem.OpenReadAsync(archivePath, cancellationToken);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            yield break;
        }

        using (archive)
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsFb2Entry(entry))
                {
                    continue;
                }

                yield return new ZipArchiveBookEntry
                {
                    ArchivePath = archivePath,
                    EntryPath = entry.FullName,
                    FileName = entry.Name,
                    FileSize = entry.Length,
                    OpenReadAsync = token => OpenEntryStreamAsync(archivePath, entry.FullName, token)
                };
            }
        }
    }

    private static bool IsFb2Entry(ZipArchiveEntry entry)
        => !string.IsNullOrWhiteSpace(entry.Name) &&
           entry.FullName.EndsWith(".fb2", StringComparison.OrdinalIgnoreCase);

    private async Task<Stream> OpenEntryStreamAsync(
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken)
    {
        var archiveStream = await _fileSystem.OpenReadAsync(archivePath, cancellationToken);

        try
        {
            var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry(entryPath);

            if (entry is null)
            {
                archive.Dispose();
                await archiveStream.DisposeAsync();
                throw new FileNotFoundException(
                    $"Archive entry '{entryPath}' was not found in '{archivePath}'.",
                    entryPath);
            }

            return new OwnedArchiveEntryStream(entry.Open(), archive, archiveStream);
        }
        catch
        {
            await archiveStream.DisposeAsync();
            throw;
        }
    }

    private sealed class OwnedArchiveEntryStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly ZipArchive _archive;
        private readonly Stream _archiveStream;

        public OwnedArchiveEntryStream(Stream innerStream, ZipArchive archive, Stream archiveStream)
        {
            _innerStream = innerStream;
            _archive = archive;
            _archiveStream = archiveStream;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush()
            => _innerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => _innerStream.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer)
            => _innerStream.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _innerStream.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => _innerStream.Seek(offset, origin);

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override void Write(ReadOnlySpan<byte> buffer)
            => throw new NotSupportedException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
                _archive.Dispose();
                _archiveStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            _archive.Dispose();
            await _archiveStream.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
