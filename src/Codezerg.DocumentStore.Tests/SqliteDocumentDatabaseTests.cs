using Codezerg.DocumentStore;
using Dapper;

namespace Codezerg.DocumentStore.Tests;

public class SqliteDocumentDatabaseTests : IDisposable
{
    private readonly string _dbFile;
    private readonly SqliteDocumentDatabase _database;

    public SqliteDocumentDatabaseTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _database = new SqliteDocumentDatabase($"Data Source={_dbFile}");
    }

    public void Dispose()
    {
        _database?.Dispose();
        if (File.Exists(_dbFile))
        {
            try { File.Delete(_dbFile); } catch { }
        }
    }

    [Fact]
    public void Constructor_ShouldCreateInMemoryDatabase()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        try
        {
            using var db = new SqliteDocumentDatabase($"Data Source={tempFile}");

            Assert.NotNull(db);
            Assert.NotEqual(":memory:", db.DatabaseName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void Constructor_ShouldCreateDatabaseWithFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        try
        {
            using (var db = new SqliteDocumentDatabase($"Data Source={tempFile}"))
            {
                Assert.NotNull(db);
                Assert.NotEqual(":memory:", db.DatabaseName);
            }

            // Verify file was created
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public async Task GetCollection_ShouldReturnCollection()
    {
        var collection = await _database.GetCollectionAsync<TestDocument>("test");

        Assert.NotNull(collection);
        Assert.Equal("test", collection.CollectionName);
    }

    [Fact]
    public async Task GetCollection_ShouldReturnSameInstanceForSameName()
    {
        var collection1 = await _database.GetCollectionAsync<TestDocument>("test");
        var collection2 = await _database.GetCollectionAsync<TestDocument>("test");

        Assert.Same(collection1, collection2);
    }

    [Fact]
    public async Task CreateCollection_ShouldCreateCollection()
    {
        await _database.CreateCollectionAsync<TestDocument>("newCollection");

        var collections = await _database.ListCollectionNamesAsync();
        Assert.Contains("newCollection", collections);
    }

    [Fact]
    public async Task DropCollection_ShouldRemoveCollection()
    {
        await _database.CreateCollectionAsync<TestDocument>("toDelete");
        var beforeDrop = await _database.ListCollectionNamesAsync();
        Assert.Contains("toDelete", beforeDrop);

        await _database.DropCollectionAsync("toDelete");

        var afterDrop = await _database.ListCollectionNamesAsync();
        Assert.DoesNotContain("toDelete", afterDrop);
    }

    [Fact]
    public async Task ListCollectionNames_ShouldReturnAllCollections()
    {
        await _database.CreateCollectionAsync<TestDocument>("collection1");
        await _database.CreateCollectionAsync<TestDocument>("collection2");
        await _database.CreateCollectionAsync<TestDocument>("collection3");

        var collections = await _database.ListCollectionNamesAsync();

        Assert.Contains("collection1", collections);
        Assert.Contains("collection2", collections);
        Assert.Contains("collection3", collections);
    }

    [Fact]
    public async Task ListCollectionNames_ShouldReturnEmptyListWhenNoCollections()
    {
        var collections = await _database.ListCollectionNamesAsync();

        Assert.NotNull(collections);
        Assert.Empty(collections);
    }

    [Fact]
    public void Constructor_ShouldApplyDefaultPragmas()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_default_pragma_{Guid.NewGuid()}.db");
        try
        {
            using var db = new SqliteDocumentDatabase($"Data Source={tempFile}");

            // Query pragma values from the database
            using (var connection = db.CreateConnection())
            {
                var journalMode = connection.QuerySingle<string>("PRAGMA journal_mode;");
                var pageSize = connection.QuerySingle<int>("PRAGMA page_size;");
                var synchronous = connection.QuerySingle<int>("PRAGMA synchronous;");

                Assert.Equal("wal", journalMode.ToLowerInvariant());
                Assert.Equal(4096, pageSize);
                Assert.Equal(1, synchronous); // NORMAL = 1
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void Constructor_ShouldApplyCustomPragmasViaOptions()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_pragma_{Guid.NewGuid()}.db");
        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new Configuration.DocumentDatabaseOptions
                {
                    ConnectionString = $"Data Source={tempFile}",
                    JournalMode = "DELETE",
                    PageSize = 8192,
                    Synchronous = "FULL"
                });

            using var db = new SqliteDocumentDatabase(options);

            // Query pragma values from the database
            using (var connection = db.CreateConnection())
            {
                var journalMode = connection.QuerySingle<string>("PRAGMA journal_mode;");
                var pageSize = connection.QuerySingle<int>("PRAGMA page_size;");
                var synchronous = connection.QuerySingle<int>("PRAGMA synchronous;");

                Assert.Equal("delete", journalMode.ToLowerInvariant());
                Assert.Equal(8192, pageSize);
                Assert.Equal(2, synchronous); // FULL = 2
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    [Fact]
    public void Constructor_ShouldAllowNullPragmaValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_null_pragma_{Guid.NewGuid()}.db");
        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new Configuration.DocumentDatabaseOptions
                {
                    ConnectionString = $"Data Source={tempFile}",
                    JournalMode = null,
                    PageSize = null,
                    Synchronous = null
                });

            using var db = new SqliteDocumentDatabase(options);

            // Should not throw exception
            Assert.NotNull(db);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
    }
}
