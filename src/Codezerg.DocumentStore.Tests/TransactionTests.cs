using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class TransactionTests : IDisposable
{
    private readonly SqliteDocumentDatabase _database;
    private readonly IDocumentCollection<TestDocument> _collection;

    public TransactionTests()
    {
        _database = new SqliteDocumentDatabase("Data Source=:memory:");
        _collection = _database.GetCollection<TestDocument>("test");
    }

    public void Dispose()
    {
        _database?.Dispose();
    }

    [Fact]
    public async Task Transaction_Commit_ShouldPersistChanges()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            var doc = new TestDocument { Name = "Test" };
            await _collection.InsertOneAsync(doc, transaction);

            await transaction.CommitAsync();
        }

        var count = await _collection.CountAllAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Transaction_Rollback_ShouldNotPersistChanges()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            var doc = new TestDocument { Name = "Test" };
            await _collection.InsertOneAsync(doc, transaction);

            await transaction.RollbackAsync();
        }

        var count = await _collection.CountAllAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Transaction_DisposeWithoutCommit_ShouldRollback()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            var doc = new TestDocument { Name = "Test" };
            await _collection.InsertOneAsync(doc, transaction);

            // Dispose without commit = automatic rollback
        }

        var count = await _collection.CountAllAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Transaction_MultipleOperations_ShouldBeAtomic()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc1" }, transaction);
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc2" }, transaction);
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc3" }, transaction);

            await transaction.CommitAsync();
        }

        var count = await _collection.CountAllAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Transaction_RollbackMultipleOperations_ShouldRevertAll()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc1" }, transaction);
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc2" }, transaction);
            await _collection.InsertOneAsync(new TestDocument { Name = "Doc3" }, transaction);

            await transaction.RollbackAsync();
        }

        var count = await _collection.CountAllAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Transaction_Query_ShouldSeeUncommittedChanges()
    {
        await using (var transaction = await _database.BeginTransactionAsync())
        {
            var doc = new TestDocument { Name = "Test" };
            await _collection.InsertOneAsync(doc, transaction);

            // Should see the document within the transaction
            var found = await _collection.FindByIdAsync(doc.Id, transaction);
            Assert.NotNull(found);
            Assert.Equal("Test", found.Name);

            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task Transaction_Update_ShouldWorkInTransaction()
    {
        var doc = new TestDocument { Name = "Original" };
        await _collection.InsertOneAsync(doc);

        await using (var transaction = await _database.BeginTransactionAsync())
        {
            doc.Name = "Updated";
            await _collection.UpdateByIdAsync(doc.Id, doc, transaction);

            await transaction.CommitAsync();
        }

        var found = await _collection.FindByIdAsync(doc.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated", found.Name);
    }

    [Fact]
    public async Task Transaction_Delete_ShouldWorkInTransaction()
    {
        var doc = new TestDocument { Name = "ToDelete" };
        await _collection.InsertOneAsync(doc);

        await using (var transaction = await _database.BeginTransactionAsync())
        {
            await _collection.DeleteByIdAsync(doc.Id, transaction);
            await transaction.CommitAsync();
        }

        var found = await _collection.FindByIdAsync(doc.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task Transaction_DoubleCommit_ShouldThrow()
    {
        await using var transaction = await _database.BeginTransactionAsync();

        await transaction.CommitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.CommitAsync());
    }

    [Fact]
    public async Task Transaction_DoubleRollback_ShouldThrow()
    {
        await using var transaction = await _database.BeginTransactionAsync();

        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.RollbackAsync());
    }

    [Fact]
    public async Task Transaction_CommitAfterRollback_ShouldThrow()
    {
        await using var transaction = await _database.BeginTransactionAsync();

        await transaction.RollbackAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await transaction.CommitAsync());
    }

    [Fact]
    public async Task Transaction_DbTransaction_ShouldNotBeNull()
    {
        await using var transaction = await _database.BeginTransactionAsync();

        Assert.NotNull(transaction.DbTransaction);
    }

    [Fact]
    public async Task Transaction_MultipleCollections_ShouldWorkTogether()
    {
        var collection1 = _database.GetCollection<TestDocument>("col1");
        var collection2 = _database.GetCollection<TestDocument>("col2");

        await using (var transaction = await _database.BeginTransactionAsync())
        {
            await collection1.InsertOneAsync(new TestDocument { Name = "Doc1" }, transaction);
            await collection2.InsertOneAsync(new TestDocument { Name = "Doc2" }, transaction);

            await transaction.CommitAsync();
        }

        Assert.Equal(1, await collection1.CountAllAsync());
        Assert.Equal(1, await collection2.CountAllAsync());
    }

    [Fact]
    public async Task Transaction_MultipleCollectionsRollback_ShouldRevertAll()
    {
        var collection1 = _database.GetCollection<TestDocument>("col1");
        var collection2 = _database.GetCollection<TestDocument>("col2");

        await using (var transaction = await _database.BeginTransactionAsync())
        {
            await collection1.InsertOneAsync(new TestDocument { Name = "Doc1" }, transaction);
            await collection2.InsertOneAsync(new TestDocument { Name = "Doc2" }, transaction);

            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await collection1.CountAllAsync());
        Assert.Equal(0, await collection2.CountAllAsync());
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
