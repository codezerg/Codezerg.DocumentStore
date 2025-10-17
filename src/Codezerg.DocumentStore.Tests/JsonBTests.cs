using Codezerg.DocumentStore;

namespace Codezerg.DocumentStore.Tests;

public class JsonBTests : IAsyncLifetime
{
    private readonly string _dbFile;
    private readonly SqliteDocumentDatabase _database;
    private IDocumentCollection<TestDocument> _collection = null!;

    public JsonBTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        // Create database with JSONB enabled
        _database = new SqliteDocumentDatabase($"Data Source={_dbFile}", useJsonB: true);
    }

    public async Task InitializeAsync()
    {
        _collection = await _database.GetCollectionAsync<TestDocument>("test");
    }

    public Task DisposeAsync()
    {
        _database?.Dispose();
        if (File.Exists(_dbFile))
        {
            try { File.Delete(_dbFile); } catch { }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InsertOne_WithJsonB_ShouldWork()
    {
        var doc = new TestDocument
        {
            Name = "Test Document",
            Age = 25,
            Email = "test@example.com"
        };

        await _collection.InsertOneAsync(doc);

        Assert.NotEqual(DocumentId.Empty, doc.Id);

        var retrieved = await _collection.FindByIdAsync(doc.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Document", retrieved.Name);
        Assert.Equal(25, retrieved.Age);
        Assert.Equal("test@example.com", retrieved.Email);
    }

    [Fact]
    public async Task UpdateById_WithJsonB_ShouldWork()
    {
        var doc = new TestDocument { Name = "Original", Age = 30 };
        await _collection.InsertOneAsync(doc);

        doc.Name = "Updated";
        doc.Age = 35;
        var updated = await _collection.UpdateByIdAsync(doc.Id, doc);

        Assert.True(updated);

        var retrieved = await _collection.FindByIdAsync(doc.Id);
        Assert.Equal("Updated", retrieved?.Name);
        Assert.Equal(35, retrieved?.Age);
    }

    [Fact]
    public async Task Find_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25 },
            new TestDocument { Name = "Bob", Age = 30 },
            new TestDocument { Name = "Charlie", Age = 35 }
        });

        var results = await _collection.FindAsync(d => d.Age >= 30);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, d => d.Name == "Bob");
        Assert.Contains(results, d => d.Name == "Charlie");
    }

    [Fact]
    public async Task FindOne_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25 },
            new TestDocument { Name = "Bob", Age = 30 }
        });

        var result = await _collection.FindOneAsync(d => d.Name == "Bob");

        Assert.NotNull(result);
        Assert.Equal("Bob", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public async Task FindAll_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25 },
            new TestDocument { Name = "Bob", Age = 30 },
            new TestDocument { Name = "Charlie", Age = 35 }
        });

        var results = await _collection.FindAllAsync();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task Count_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25 },
            new TestDocument { Name = "Bob", Age = 30 },
            new TestDocument { Name = "Charlie", Age = 35 }
        });

        var count = await _collection.CountAsync(d => d.Age >= 30);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task DeleteById_WithJsonB_ShouldWork()
    {
        var doc = new TestDocument { Name = "ToDelete", Age = 40 };
        await _collection.InsertOneAsync(doc);

        var deleted = await _collection.DeleteByIdAsync(doc.Id);

        Assert.True(deleted);

        var retrieved = await _collection.FindByIdAsync(doc.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteMany_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25 },
            new TestDocument { Name = "Bob", Age = 30 },
            new TestDocument { Name = "Charlie", Age = 35 }
        });

        var deletedCount = await _collection.DeleteManyAsync(d => d.Age >= 30);

        Assert.Equal(2, deletedCount);

        var remaining = await _collection.FindAllAsync();
        Assert.Single(remaining);
        Assert.Equal("Alice", remaining[0].Name);
    }

    [Fact]
    public async Task CreateIndex_WithJsonB_ShouldWork()
    {
        await _collection.CreateIndexAsync(d => d.Email, unique: true);

        var doc1 = new TestDocument { Name = "User1", Email = "unique@example.com" };
        await _collection.InsertOneAsync(doc1);

        var doc2 = new TestDocument { Name = "User2", Email = "unique@example.com" };
        await Assert.ThrowsAsync<Exceptions.DuplicateKeyException>(() =>
            _collection.InsertOneAsync(doc2));
    }

    [Fact]
    public async Task Pagination_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Doc1", Age = 1 },
            new TestDocument { Name = "Doc2", Age = 2 },
            new TestDocument { Name = "Doc3", Age = 3 },
            new TestDocument { Name = "Doc4", Age = 4 },
            new TestDocument { Name = "Doc5", Age = 5 }
        });

        var page1 = await _collection.FindAsync(d => d.Age > 0, skip: 0, limit: 2);
        var page2 = await _collection.FindAsync(d => d.Age > 0, skip: 2, limit: 2);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public async Task ComplexQuery_WithJsonB_ShouldWork()
    {
        await _collection.InsertManyAsync(new[]
        {
            new TestDocument { Name = "Alice", Age = 25, Email = "alice@example.com" },
            new TestDocument { Name = "Bob", Age = 30, Email = "bob@example.com" },
            new TestDocument { Name = "Charlie", Age = 35, Email = "charlie@example.com" }
        });

        var results = await _collection.FindAsync(d => d.Age > 25 && d.Email.Contains("@example.com"));

        Assert.Equal(2, results.Count);
        Assert.DoesNotContain(results, d => d.Name == "Alice");
    }

    private class TestDocument
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Email { get; set; }
    }
}
