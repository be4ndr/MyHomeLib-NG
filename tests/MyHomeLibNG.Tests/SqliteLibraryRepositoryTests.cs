using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Repositories;
using MyHomeLibNG.Infrastructure.Services;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class SqliteLibraryRepositoryTests
{
    private const string MockRootDirectory = @"X:\mock-library";
    private const string MockInpxFilePath = @"X:\mock-library\sample.inpx";

    [Fact]
    public async Task AddAndGetById_PersistsOnlineLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var createdAtUtc = DateTimeOffset.UtcNow;
            var newId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Flibusta",
                ProviderId = BookProviderIds.ProjectGutenberg,
                LibraryType = LibraryType.Online,
                OnlineSource = new OnlineLibrarySourceSettings
                {
                    ApiBaseUrl = "https://flibusta.lib/api",
                    SearchEndpoint = "https://flibusta.lib/api/search"
                },
                CreatedAtUtc = createdAtUtc
            });

            var loaded = await repository.GetByIdAsync(newId);

            Assert.NotNull(loaded);
            Assert.Equal("Flibusta", loaded.Name);
            Assert.Equal(LibraryType.Online, loaded.LibraryType);
            Assert.NotNull(loaded.OnlineSource);
            Assert.Equal("https://flibusta.lib/api", loaded.OnlineSource.ApiBaseUrl);
            Assert.Equal("https://flibusta.lib/api/search", loaded.OnlineSource.SearchEndpoint);
            Assert.Null(loaded.FolderSource);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task AddAndGetById_PersistsFolderLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var newId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Local archive",
                ProviderId = BookProviderIds.OfflineInpx,
                LibraryType = LibraryType.Folder,
                FolderSource = new FolderLibrarySourceSettings
                {
                    InpxFilePath = MockInpxFilePath,
                    ArchiveDirectoryPath = MockRootDirectory
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            var loaded = await repository.GetByIdAsync(newId);

            Assert.NotNull(loaded);
            Assert.Equal(LibraryType.Folder, loaded.LibraryType);
            Assert.NotNull(loaded.FolderSource);
            Assert.Equal(MockInpxFilePath, loaded.FolderSource.InpxFilePath);
            Assert.Equal(MockRootDirectory, loaded.FolderSource.ArchiveDirectoryPath);
            Assert.Null(loaded.OnlineSource);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesLibraryProfile()
    {
        var database = await CreateDatabaseAsync();

        try
        {
            var repository = new SqliteLibraryRepository(database.ConnectionString);
            var libraryId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Delete me",
                ProviderId = BookProviderIds.ProjectGutenberg,
                LibraryType = LibraryType.Online,
                OnlineSource = new OnlineLibrarySourceSettings
                {
                    ApiBaseUrl = "https://example.test/api",
                    SearchEndpoint = "https://example.test/api/search"
                },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            await repository.DeleteAsync(libraryId);

            var profiles = await repository.GetLibraryProfilesAsync();
            var loaded = await repository.GetByIdAsync(libraryId);

            Assert.DoesNotContain(profiles, profile => profile.Id == libraryId);
            Assert.Null(loaded);
        }
        finally
        {
            database.Dispose();
        }
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOnlineApiStructure()
    {
        var resolver = new LibrarySourceResolver(CreateOnlineEnvironment());
        var structure = await resolver.ResolveAsync(CreateOnlineProfile());

        Assert.Equal(LibraryType.Online, structure.LibraryType);
        Assert.Equal(2, structure.Sources.Count);
        Assert.All(structure.Sources, source => Assert.Equal(SourceKind.Http, source.Kind));
        Assert.All(structure.Sources, source => Assert.True(source.Exists));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOfflineInpxAndArchiveStructure()
    {
        var resolver = new LibrarySourceResolver(CreateOfflineEnvironment());
        var structure = await resolver.ResolveAsync(CreateFolderProfile());

        Assert.Equal(LibraryType.Folder, structure.LibraryType);
        Assert.Equal(4, structure.Sources.Count);

        var inpxSource = structure.Sources.Single(source => source.Description == "Offline INPX index");
        Assert.Equal(SourceKind.FileSystem, inpxSource.Kind);
        Assert.True(inpxSource.Exists);
        Assert.Equal(100, inpxSource.SizeBytes);

        var archiveSources = structure.Sources.Where(source => source.Kind == SourceKind.Archive).ToArray();
        Assert.Equal(3, archiveSources.Length);
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(archiveSources, source => source.PathOrUri.EndsWith(".gz", StringComparison.OrdinalIgnoreCase));
        Assert.All(archiveSources, source => Assert.True(source.Exists));
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-test-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";
        var initializer = new SqliteSchemaInitializer(connectionString);
        await initializer.InitializeAsync();
        return new TestDatabase(dbPath, connectionString);
    }

    private static LibraryProfile CreateOnlineProfile()
    {
        return new LibraryProfile
        {
            Id = 17,
            Name = "Project Gutenberg",
            ProviderId = BookProviderIds.ProjectGutenberg,
            LibraryType = LibraryType.Online,
            OnlineSource = new OnlineLibrarySourceSettings
            {
                ApiBaseUrl = "https://gutendex.com",
                SearchEndpoint = "https://gutendex.com/books"
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static LibraryProfile CreateFolderProfile()
    {
        return new LibraryProfile
        {
            Id = 42,
            Name = "Offline sample",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = MockInpxFilePath,
                ArchiveDirectoryPath = MockRootDirectory
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static MockLibrarySourceEnvironment CreateOnlineEnvironment()
    {
        var environment = new MockLibrarySourceEnvironment();
        environment.ValidUris.Add("https://gutendex.com");
        environment.ValidUris.Add("https://gutendex.com/books");
        return environment;
    }

    private static MockLibrarySourceEnvironment CreateOfflineEnvironment()
    {
        var environment = new MockLibrarySourceEnvironment();
        environment.ExistingDirectories.Add(MockRootDirectory);
        environment.ExistingFiles[MockInpxFilePath] = 100;
        environment.ExistingFiles[$"{MockRootDirectory}\\a.fb2-000001-000100.zip"] = 200;
        environment.ExistingFiles[$"{MockRootDirectory}\\b.fb2-000101-000200.7z"] = 300;
        environment.ExistingFiles[$"{MockRootDirectory}\\c.fb2-000201-000300.gz"] = 400;
        environment.ExistingFiles[$"{MockRootDirectory}\\ignore.txt"] = 500;
        return environment;
    }

    private sealed class TestDatabase : IDisposable
    {
        public TestDatabase(string databasePath, string connectionString)
        {
            DatabasePath = databasePath;
            ConnectionString = connectionString;
        }

        public string DatabasePath { get; }
        public string ConnectionString { get; }

        public void Dispose()
        {
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }
    }

    private sealed class MockLibrarySourceEnvironment : ILibrarySourceEnvironment
    {
        public HashSet<string> ValidUris { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsValidAbsoluteUri(string value)
            => ValidUris.Contains(value);

        public bool FileExists(string path)
            => ExistingFiles.ContainsKey(path);

        public long? GetFileSize(string path)
            => ExistingFiles.TryGetValue(path, out var sizeBytes) ? sizeBytes : null;

        public bool DirectoryExists(string path)
            => ExistingDirectories.Contains(path);

        public IReadOnlyList<string> EnumerateFiles(string directoryPath)
            // The production implementation scans a single directory, so the mock
            // mirrors that contract instead of trying to emulate a real file system.
            => ExistingFiles.Keys.Where(path => string.Equals(Path.GetDirectoryName(path), directoryPath, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
