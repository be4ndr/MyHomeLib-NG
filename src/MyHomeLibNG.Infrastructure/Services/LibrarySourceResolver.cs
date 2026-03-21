using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Interfaces;
using MyHomeLibNG.Core.Models;

namespace MyHomeLibNG.Infrastructure.Services;

public sealed class LibrarySourceResolver : ILibrarySourceResolver
{
    private static readonly string[] SupportedArchiveExtensions = [".zip", ".gz", ".gzip", ".7z", ".rar", ".tar"];
    private const string InpxDescription = "Offline INPX index";
    private readonly ILibrarySourceEnvironment _environment;

    public LibrarySourceResolver()
        : this(new LibrarySourceEnvironment())
    {
    }

    public LibrarySourceResolver(ILibrarySourceEnvironment environment)
    {
        _environment = environment;
    }

    public Task<LibraryStructure> ResolveAsync(LibraryProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var structure = profile.LibraryType switch
        {
            LibraryType.Online => BuildOnlineStructure(profile),
            LibraryType.Folder => BuildFolderStructure(profile),
            _ => throw new NotSupportedException($"Library type '{profile.LibraryType}' is not supported.")
        };

        return Task.FromResult(structure);
    }

    private LibraryStructure BuildOnlineStructure(LibraryProfile profile)
    {
        var source = profile.OnlineSource
            ?? throw new InvalidOperationException("Online library profiles require online source settings.");

        if (string.IsNullOrWhiteSpace(source.ApiBaseUrl))
        {
            throw new InvalidOperationException("Online library profiles require an API base URL.");
        }

        var sources = new List<LibrarySourceLocation>
        {
            CreateHttpLocation(source.ApiBaseUrl, "Online API base URL")
        };

        if (!string.IsNullOrWhiteSpace(source.SearchEndpoint))
        {
            sources.Add(CreateHttpLocation(source.SearchEndpoint, "Online search endpoint"));
        }

        return CreateStructure(profile, sources);
    }

    private LibraryStructure BuildFolderStructure(LibraryProfile profile)
    {
        var source = profile.FolderSource
            ?? throw new InvalidOperationException("Folder library profiles require folder source settings.");

        if (string.IsNullOrWhiteSpace(source.InpxFilePath))
        {
            throw new InvalidOperationException("Folder library profiles require an INPX file path.");
        }

        if (string.IsNullOrWhiteSpace(source.ArchiveDirectoryPath))
        {
            throw new InvalidOperationException("Folder library profiles require an archive directory path.");
        }

        var sources = new List<LibrarySourceLocation>
        {
            CreateFileLocation(source.InpxFilePath, InpxDescription, SourceKind.FileSystem)
        };

        if (_environment.DirectoryExists(source.ArchiveDirectoryPath))
        {
            sources.AddRange(GetArchiveLocations(source.ArchiveDirectoryPath));
        }
        else
        {
            sources.Add(new LibrarySourceLocation
            {
                Kind = SourceKind.Archive,
                PathOrUri = source.ArchiveDirectoryPath,
                Description = "Offline archive directory",
                Exists = false
            });
        }

        return CreateStructure(profile, sources);
    }

    private IReadOnlyList<LibrarySourceLocation> GetArchiveLocations(string directoryPath)
    {
        return _environment
            .EnumerateFiles(directoryPath)
            .Where(IsSupportedArchive)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(CreateArchiveLocation)
            .ToArray();
    }

    private static bool IsSupportedArchive(string path)
        => SupportedArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private LibrarySourceLocation CreateArchiveLocation(string path)
    {
        return new LibrarySourceLocation
        {
            Kind = SourceKind.Archive,
            PathOrUri = path,
            Description = $"Offline archive ({Path.GetExtension(path)})",
            Exists = _environment.FileExists(path),
            SizeBytes = _environment.GetFileSize(path)
        };
    }

    private LibrarySourceLocation CreateHttpLocation(string uri, string description)
    {
        return new LibrarySourceLocation
        {
            Kind = SourceKind.Http,
            PathOrUri = uri,
            Description = description,
            Exists = _environment.IsValidAbsoluteUri(uri)
        };
    }

    private LibrarySourceLocation CreateFileLocation(string path, string description, SourceKind kind)
    {
        return new LibrarySourceLocation
        {
            Kind = kind,
            PathOrUri = path,
            Description = description,
            Exists = _environment.FileExists(path),
            SizeBytes = _environment.GetFileSize(path)
        };
    }

    private static LibraryStructure CreateStructure(LibraryProfile profile, IReadOnlyList<LibrarySourceLocation> sources)
    {
        return new LibraryStructure
        {
            LibraryProfileId = profile.Id,
            LibraryType = profile.LibraryType,
            Sources = sources
        };
    }
}
