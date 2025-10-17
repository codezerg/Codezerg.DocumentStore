using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class SqliteDocumentDatabaseTests : IDisposable
{
    private readonly SqliteDocumentDatabase _database;

    public SqliteDocumentDatabaseTests()
    {
        _database = new SqliteDocumentDatabase("Data Source=:memory:");
    }

    public void Dispose()
    {
        _database?.Dispose();
    }

    [Fact]
    public void Constructor_ShouldCreateInMemoryDatabase()
    {
        using var db = new SqliteDocumentDatabase("Data Source=:memory:");

        Assert.NotNull(db);
        Assert.Equal(":memory:", db.DatabaseName);
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
    public void GetCollection_ShouldReturnCollection()
    {
        var collection = _database.GetCollection<TestDocument>("test");

        Assert.NotNull(collection);
        Assert.Equal("test", collection.CollectionName);
    }

    [Fact]
    public void GetCollection_ShouldReturnSameInstanceForSameName()
    {
        var collection1 = _database.GetCollection<TestDocument>("test");
        var collection2 = _database.GetCollection<TestDocument>("test");

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
    public async Task BeginTransaction_ShouldReturnTransaction()
    {
        using var transaction = await _database.BeginTransactionAsync();

        Assert.NotNull(transaction);
        Assert.NotNull(transaction.DbTransaction);
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
    }
}
