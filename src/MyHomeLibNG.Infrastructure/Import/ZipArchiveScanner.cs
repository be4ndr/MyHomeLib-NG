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

            await foreach (var entry in ScanArchiveAsync(archivePath, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    private async IAsyncEnumerable<ZipArchiveBookEntry> ScanArchiveAsync(
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
                if (string.IsNullOrWhiteSpace(entry.Name) ||
                    !entry.FullName.EndsWith(".fb2", StringComparison.OrdinalIgnoreCase))
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

    private IReadOnlyList<string> ResolveArchivePaths(string path)
    {
        if (_fileSystem.FileExists(path))
        {
            return path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? [path]
                : Array.Empty<string>();
        }

        return _fileSystem.EnumerateFilesRecursive(path)
            .Where(candidate => candidate.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<Stream> OpenEntryStreamAsync(
        string archivePath,
        string entryPath,
        CancellationToken cancellationToken)
    {
        var archiveStream = await _fileSystem.OpenReadAsync(archivePath, cancellationToken);

        try
        {
            var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry(entryPath) ??
                        archive.Entries.FirstOrDefault(candidate =>
                            string.Equals(candidate.FullName, entryPath, StringComparison.Ordinal));

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
