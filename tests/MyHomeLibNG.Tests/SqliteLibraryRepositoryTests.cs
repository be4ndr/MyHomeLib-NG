using MyHomeLibNG.Core.Enums;
using MyHomeLibNG.Core.Models;
using MyHomeLibNG.Infrastructure.Data;
using MyHomeLibNG.Infrastructure.Repositories;
using Xunit;

namespace MyHomeLibNG.Tests;

public class SqliteLibraryRepositoryTests
{
    [Fact]
    public async Task AddAndGetById_Works()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"MyHomeLibNG-test-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Pooling=False";

        try
        {
            var initializer = new SqliteSchemaInitializer(connectionString);
            await initializer.InitializeAsync();

            var repository = new SqliteLibraryRepository(connectionString);
            var newId = await repository.AddAsync(new LibraryProfile
            {
                Name = "Primary library",
                LibraryType = LibraryType.Local,
                ConnectionInfo = "/books",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            var loaded = await repository.GetByIdAsync(newId);

            Assert.NotNull(loaded);
            Assert.Equal("Primary library", loaded.Name);
            Assert.Equal(LibraryType.Local, loaded.LibraryType);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
