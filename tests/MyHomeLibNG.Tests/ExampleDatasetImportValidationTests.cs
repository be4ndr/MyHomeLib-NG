using MyHomeLibNG.Application;
using MyHomeLibNG.Core.Constants;
using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Import;
using MyHomeLibNG.Infrastructure.Providers.Offline;
using MyHomeLibNG.Infrastructure.Repositories;
using Xunit;

namespace MyHomeLibNG.Tests;

public sealed class ExampleDatasetImportValidationTests
{
    [Fact]
    public async Task ImportSmallestExampleArchive_IndexesAndIsSearchable()
    {
        var repoRoot = FindRepoRoot();
        var exampleDirectory = Path.Combine(repoRoot, "example");
        if (!Directory.Exists(exampleDirectory))
        {
            return;
        }

        var smallestArchive = new DirectoryInfo(exampleDirectory)
            .EnumerateFiles("*.zip", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file.Length)
            .FirstOrDefault();
        if (smallestArchive is null)
        {
            return;
        }

        var inpxPath = Path.Combine(exampleDirectory, "flibusta_fb2_local.inpx");
        using var database = await CreateDatabaseAsync();
        var repository = new SqliteLibraryRepository(database.ConnectionString);
        var service = new BookImportService(
            new ZipArchiveScanner(new OfflineLibraryFileSystem()),
            new Fb2MetadataParser(),
            repository,
            archiveWorkerCount: 3,
            inpxLibraryIndexReader: new InpxLibraryIndexReader(new InpxCatalogParser(), new OfflineLibraryFileSystem()));

        var profile = new LibraryProfile
        {
            Id = 901,
            Name = "Example Validation",
            ProviderId = BookProviderIds.OfflineInpx,
            LibraryType = LibraryType.Folder,
            FolderSource = new FolderLibrarySourceSettings
            {
                InpxFilePath = inpxPath,
                ArchiveDirectoryPath = exampleDirectory
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var summary = await service.ImportLibraryAsync(profile);
        var indexedCount = await repository.GetImportedBookCountAsync(profile.Id);
        Assert.True(summary.ImportedCount > 0);
        Assert.Equal(summary.ImportedCount, indexedCount);

        var allBooks = await repository.SearchImportedBooksAsync(profile.Id, string.Empty);
        Assert.NotEmpty(allBooks);

        var sample = allBooks.First();
        var titleToken = FirstToken(sample.Title);
        Assert.False(string.IsNullOrWhiteSpace(titleToken));
        var titleResults = await repository.SearchImportedBooksAsync(profile.Id, titleToken!);
        Assert.NotEmpty(titleResults);

        var authorToken = FirstToken(sample.Authors);
        IReadOnlyList<ImportedBookMetadataSnapshot>? authorResults = null;
        if (!string.IsNullOrWhiteSpace(authorToken))
        {
            authorResults = await repository.SearchImportedBooksAsync(profile.Id, authorToken);
            Assert.NotEmpty(authorResults);
        }

        var genreToken = FirstToken(sample.Genres);
        IReadOnlyList<ImportedBookMetadataSnapshot>? genreResults = null;
        if (!string.IsNullOrWhiteSpace(genreToken))
        {
            genreResults = await repository.SearchImportedBooksAsync(profile.Id, genreToken);
            Assert.NotEmpty(genreResults);
        }

        var provider = new OfflineBookProvider(
            profile,
            repository,
            new InpxCatalogParser(),
            new OfflineLibraryFileSystem(),
            new OfflineBookLocationResolver(new OfflineLibraryFileSystem()),
            new OfflineContentStorageRegistry(
            [
                new FileSystemContentStorage(new OfflineLibraryFileSystem()),
                new ZipContentStorage(new OfflineLibraryFileSystem())
            ]));

        var providerSearchResults = await provider.SearchAsync(titleToken!);
        Assert.NotEmpty(providerSearchResults);

        Console.WriteLine($"Example archive: {smallestArchive.Name}");
        Console.WriteLine($"Indexed count: {indexedCount}");
        Console.WriteLine($"Search example (title): {titleToken}");
        Console.WriteLine($"Search result count (title): {titleResults.Count}");
        if (!string.IsNullOrWhiteSpace(authorToken) && authorResults is not null)
        {
            Console.WriteLine($"Search example (author): {authorToken}");
            Console.WriteLine($"Search result count (author): {authorResults.Count}");
        }

        if (!string.IsNullOrWhiteSpace(genreToken) && genreResults is not null)
        {
            Console.WriteLine($"Search example (genre): {genreToken}");
            Console.WriteLine($"Search result count (genre): {genreResults.Count}");
        }
    }

    private static string? FirstToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyHomeLibNG.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-example-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";
        var initializer = new SqliteSchemaInitializer(connectionString);
        await initializer.InitializeAsync();
        return new TestDatabase(dbPath, connectionString);
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
}
