using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class TransactionTests : IDisposable
{
    private readonly SqliteDocumentDatabase _database;
    private readonly IDocumentCollection<TestDocument> _collection;

    public TransactionTests()
    {
        _database = SqliteDocumentDatabase.CreateInMemory();
        _collection = _database.GetCollection<TestDocument>("test");
    }

    public void Dispose()
    {
        _database?.Dispose();
    }

    [Fact]
    public void Transaction_Commit_ShouldPersistChanges()
    {
        using (var transaction = _database.BeginTransaction())
        {
            var doc = new TestDocument { Name = "Test" };
            _collection.InsertOne(doc, transaction);

            transaction.Commit();
        }

        var count = _collection.CountAll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Transaction_Rollback_ShouldNotPersistChanges()
    {
        using (var transaction = _database.BeginTransaction())
        {
            var doc = new TestDocument { Name = "Test" };
            _collection.InsertOne(doc, transaction);

            transaction.Rollback();
        }

        var count = _collection.CountAll();
        Assert.Equal(0, count);
    }

    [Fact]
    public void Transaction_DisposeWithoutCommit_ShouldRollback()
    {
        using (var transaction = _database.BeginTransaction())
        {
            var doc = new TestDocument { Name = "Test" };
            _collection.InsertOne(doc, transaction);

            // Dispose without commit = automatic rollback
        }

        var count = _collection.CountAll();
        Assert.Equal(0, count);
    }

    [Fact]
    public void Transaction_MultipleOperations_ShouldBeAtomic()
    {
        using (var transaction = _database.BeginTransaction())
        {
            _collection.InsertOne(new TestDocument { Name = "Doc1" }, transaction);
            _collection.InsertOne(new TestDocument { Name = "Doc2" }, transaction);
            _collection.InsertOne(new TestDocument { Name = "Doc3" }, transaction);

            transaction.Commit();
        }

        var count = _collection.CountAll();
        Assert.Equal(3, count);
    }

    [Fact]
    public void Transaction_RollbackMultipleOperations_ShouldRevertAll()
    {
        using (var transaction = _database.BeginTransaction())
        {
            _collection.InsertOne(new TestDocument { Name = "Doc1" }, transaction);
            _collection.InsertOne(new TestDocument { Name = "Doc2" }, transaction);
            _collection.InsertOne(new TestDocument { Name = "Doc3" }, transaction);

            transaction.Rollback();
        }

        var count = _collection.CountAll();
        Assert.Equal(0, count);
    }

    [Fact]
    public void Transaction_Query_ShouldSeeUncommittedChanges()
    {
        using (var transaction = _database.BeginTransaction())
        {
            var doc = new TestDocument { Name = "Test" };
            _collection.InsertOne(doc, transaction);

            // Should see the document within the transaction
            var found = _collection.FindById(doc.Id, transaction);
            Assert.NotNull(found);
            Assert.Equal("Test", found.Name);

            transaction.Rollback();
        }
    }

    [Fact]
    public void Transaction_Update_ShouldWorkInTransaction()
    {
        var doc = new TestDocument { Name = "Original" };
        _collection.InsertOne(doc);

        using (var transaction = _database.BeginTransaction())
        {
            doc.Name = "Updated";
            _collection.UpdateById(doc.Id, doc, transaction);

            transaction.Commit();
        }

        var found = _collection.FindById(doc.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated", found.Name);
    }

    [Fact]
    public void Transaction_Delete_ShouldWorkInTransaction()
    {
        var doc = new TestDocument { Name = "ToDelete" };
        _collection.InsertOne(doc);

        using (var transaction = _database.BeginTransaction())
        {
            _collection.DeleteById(doc.Id, transaction);
            transaction.Commit();
        }

        var found = _collection.FindById(doc.Id);
        Assert.Null(found);
    }

    [Fact]
    public void Transaction_DoubleCommit_ShouldThrow()
    {
        using var transaction = _database.BeginTransaction();

        transaction.Commit();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    [Fact]
    public void Transaction_DoubleRollback_ShouldThrow()
    {
        using var transaction = _database.BeginTransaction();

        transaction.Rollback();

        Assert.Throws<InvalidOperationException>(() => transaction.Rollback());
    }

    [Fact]
    public void Transaction_CommitAfterRollback_ShouldThrow()
    {
        using var transaction = _database.BeginTransaction();

        transaction.Rollback();

        Assert.Throws<InvalidOperationException>(() => transaction.Commit());
    }

    [Fact]
    public void Transaction_DbTransaction_ShouldNotBeNull()
    {
        using var transaction = _database.BeginTransaction();

        Assert.NotNull(transaction.DbTransaction);
    }

    [Fact]
    public void Transaction_MultipleCollections_ShouldWorkTogether()
    {
        var collection1 = _database.GetCollection<TestDocument>("col1");
        var collection2 = _database.GetCollection<TestDocument>("col2");

        using (var transaction = _database.BeginTransaction())
        {
            collection1.InsertOne(new TestDocument { Name = "Doc1" }, transaction);
            collection2.InsertOne(new TestDocument { Name = "Doc2" }, transaction);

            transaction.Commit();
        }

        Assert.Equal(1, collection1.CountAll());
        Assert.Equal(1, collection2.CountAll());
    }

    [Fact]
    public void Transaction_MultipleCollectionsRollback_ShouldRevertAll()
    {
        var collection1 = _database.GetCollection<TestDocument>("col1");
        var collection2 = _database.GetCollection<TestDocument>("col2");

        using (var transaction = _database.BeginTransaction())
        {
            collection1.InsertOne(new TestDocument { Name = "Doc1" }, transaction);
            collection2.InsertOne(new TestDocument { Name = "Doc2" }, transaction);

            transaction.Rollback();
        }

        Assert.Equal(0, collection1.CountAll());
        Assert.Equal(0, collection2.CountAll());
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
