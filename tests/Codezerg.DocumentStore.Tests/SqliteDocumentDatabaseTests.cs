using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class SqliteDocumentDatabaseTests : IDisposable
{
    private readonly SqliteDocumentDatabase _database;

    public SqliteDocumentDatabaseTests()
    {
        _database = SqliteDocumentDatabase.CreateInMemory();
    }

    public void Dispose()
    {
        _database?.Dispose();
    }

    [Fact]
    public void CreateInMemory_ShouldCreateDatabase()
    {
        using var db = SqliteDocumentDatabase.CreateInMemory();

        Assert.NotNull(db);
        Assert.Equal(":memory:", db.DatabaseName);
    }

    [Fact]
    public void Create_ShouldCreateDatabaseWithFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        try
        {
            using (var db = SqliteDocumentDatabase.Create(tempFile))
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
    public void CreateCollection_ShouldCreateCollection()
    {
        _database.CreateCollection<TestDocument>("newCollection");

        var collections = _database.ListCollectionNames();
        Assert.Contains("newCollection", collections);
    }

    [Fact]
    public void DropCollection_ShouldRemoveCollection()
    {
        _database.CreateCollection<TestDocument>("toDelete");
        var beforeDrop = _database.ListCollectionNames();
        Assert.Contains("toDelete", beforeDrop);

        _database.DropCollection("toDelete");

        var afterDrop = _database.ListCollectionNames();
        Assert.DoesNotContain("toDelete", afterDrop);
    }

    [Fact]
    public void ListCollectionNames_ShouldReturnAllCollections()
    {
        _database.CreateCollection<TestDocument>("collection1");
        _database.CreateCollection<TestDocument>("collection2");
        _database.CreateCollection<TestDocument>("collection3");

        var collections = _database.ListCollectionNames();

        Assert.Contains("collection1", collections);
        Assert.Contains("collection2", collections);
        Assert.Contains("collection3", collections);
    }

    [Fact]
    public void ListCollectionNames_ShouldReturnEmptyListWhenNoCollections()
    {
        var collections = _database.ListCollectionNames();

        Assert.NotNull(collections);
        Assert.Empty(collections);
    }

    [Fact]
    public void BeginTransaction_ShouldReturnTransaction()
    {
        using var transaction = _database.BeginTransaction();

        Assert.NotNull(transaction);
        Assert.NotNull(transaction.DbTransaction);
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
    }
}
