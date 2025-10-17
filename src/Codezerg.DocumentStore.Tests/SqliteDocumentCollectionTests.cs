using Codezerg.DocumentStore;
using Codezerg.DocumentStore.Exceptions;

namespace Codezerg.DocumentStore.Tests;

public class SqliteDocumentCollectionTests : IAsyncLifetime
{
    private readonly string _dbFile;
    private readonly SqliteDocumentDatabase _database;
    private IDocumentCollection<TestUser> _users = null!;

    public SqliteDocumentCollectionTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _database = new SqliteDocumentDatabase($"Data Source={_dbFile}");
    }

    public async Task InitializeAsync()
    {
        _users = await _database.GetCollectionAsync<TestUser>("users");
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
    public async Task InsertOne_ShouldInsertDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };

        await _users.InsertOneAsync(user);

        Assert.NotEqual(DocumentId.Empty, user.Id);
        Assert.NotEqual(default, user.CreatedAt);
        Assert.NotEqual(default, user.UpdatedAt);
    }

    [Fact]
    public async Task InsertOne_ShouldThrowOnDuplicateId()
    {
        var user1 = new TestUser { Name = "Alice", Age = 30 };
        await _users.InsertOneAsync(user1);

        var user2 = new TestUser { Id = user1.Id, Name = "Bob", Age = 25 };

        await Assert.ThrowsAsync<DuplicateKeyException>(() => _users.InsertOneAsync(user2));
    }

    [Fact]
    public async Task InsertMany_ShouldInsertMultipleDocuments()
    {
        var users = new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        };

        await _users.InsertManyAsync(users);

        var count = await _users.CountAllAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task FindById_ShouldReturnDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        await _users.InsertOneAsync(user);

        var found = await _users.FindByIdAsync(user.Id);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal("Alice", found.Name);
        Assert.Equal(30, found.Age);
    }

    [Fact]
    public async Task FindById_ShouldReturnNullForNonExistent()
    {
        var found = await _users.FindByIdAsync(DocumentId.NewId());

        Assert.Null(found);
    }

    [Fact]
    public async Task FindOne_ShouldReturnFirstMatch()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var found = await _users.FindOneAsync(u => u.Age == 30);

        Assert.NotNull(found);
        Assert.Equal(30, found.Age);
    }

    [Fact]
    public async Task FindOne_ShouldReturnNullWhenNoMatch()
    {
        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });

        var found = await _users.FindOneAsync(u => u.Age == 99);

        Assert.Null(found);
    }

    [Fact]
    public async Task Find_ShouldReturnMatchingDocuments()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var found = await _users.FindAsync(u => u.Age == 30);

        Assert.Equal(2, found.Count);
        Assert.All(found, u => Assert.Equal(30, u.Age));
    }

    [Fact]
    public async Task Find_WithPagination_ShouldReturnCorrectPage()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "User1", Age = 20 },
            new TestUser { Name = "User2", Age = 20 },
            new TestUser { Name = "User3", Age = 20 },
            new TestUser { Name = "User4", Age = 20 },
            new TestUser { Name = "User5", Age = 20 }
        });

        var page1 = await _users.FindAsync(u => u.Age == 20, skip: 0, limit: 2);
        var page2 = await _users.FindAsync(u => u.Age == 20, skip: 2, limit: 2);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public async Task FindAll_ShouldReturnAllDocuments()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var all = await _users.FindAllAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Count_ShouldReturnMatchingCount()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var count = await _users.CountAsync(u => u.Age == 30);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CountAll_ShouldReturnTotalCount()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var count = await _users.CountAllAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task UpdateById_ShouldUpdateDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        await _users.InsertOneAsync(user);

        user.Name = "Alice Updated";
        user.Age = 31;
        var updated = await _users.UpdateByIdAsync(user.Id, user);

        Assert.True(updated);

        var found = await _users.FindByIdAsync(user.Id);
        Assert.NotNull(found);
        Assert.Equal("Alice Updated", found.Name);
        Assert.Equal(31, found.Age);
    }

    [Fact]
    public async Task UpdateById_ShouldReturnFalseForNonExistent()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };

        var updated = await _users.UpdateByIdAsync(DocumentId.NewId(), user);

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateOne_ShouldUpdateFirstMatch()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 }
        });

        var newUser = new TestUser { Name = "Updated", Age = 31 };
        var updated = await _users.UpdateOneAsync(u => u.Age == 30, newUser);

        Assert.True(updated);

        var count = await _users.CountAsync(u => u.Age == 31);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateMany_ShouldUpdateAllMatches()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 },
            new TestUser { Name = "Carol", Age = 25 }
        });

        var updateCount = await _users.UpdateManyAsync(u => u.Age == 30, user => user.Age = 31);

        Assert.Equal(2, updateCount);

        var count = await _users.CountAsync(u => u.Age == 31);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task DeleteById_ShouldDeleteDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        await _users.InsertOneAsync(user);

        var deleted = await _users.DeleteByIdAsync(user.Id);

        Assert.True(deleted);

        var found = await _users.FindByIdAsync(user.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteById_ShouldReturnFalseForNonExistent()
    {
        var deleted = await _users.DeleteByIdAsync(DocumentId.NewId());

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteOne_ShouldDeleteFirstMatch()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 }
        });

        var deleted = await _users.DeleteOneAsync(u => u.Age == 30);

        Assert.True(deleted);

        var count = await _users.CountAllAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeleteMany_ShouldDeleteAllMatches()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 },
            new TestUser { Name = "Carol", Age = 25 }
        });

        var deleteCount = await _users.DeleteManyAsync(u => u.Age == 30);

        Assert.Equal(2, deleteCount);

        var count = await _users.CountAllAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Any_ShouldReturnTrueWhenMatches()
    {
        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });

        var exists = await _users.AnyAsync(u => u.Age == 30);

        Assert.True(exists);
    }

    [Fact]
    public async Task Any_ShouldReturnFalseWhenNoMatches()
    {
        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });

        var exists = await _users.AnyAsync(u => u.Age == 99);

        Assert.False(exists);
    }

    [Fact]
    public async Task CreateIndex_ShouldCreateIndex()
    {
        await _users.CreateIndexAsync(u => u.Name);

        // Insert and query to verify index works
        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });
        var found = await _users.FindOneAsync(u => u.Name == "Alice");

        Assert.NotNull(found);
    }

    [Fact]
    public async Task CreateIndex_Unique_ShouldEnforceUniqueness()
    {
        await _users.CreateIndexAsync(u => u.Name, unique: true);

        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });

        // This should fail due to unique constraint
        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 25 }));
    }

    [Fact]
    public async Task DropIndex_ShouldDropIndex()
    {
        await _users.CreateIndexAsync(u => u.Name);
        await _users.DropIndexAsync(u => u.Name);

        // Should still work without index
        await _users.InsertOneAsync(new TestUser { Name = "Alice", Age = 30 });
        var found = await _users.FindOneAsync(u => u.Name == "Alice");

        Assert.NotNull(found);
    }

    [Fact]
    public async Task Query_StringContains_ShouldWork()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Alicia", Age = 35 }
        });

        var found = await _users.FindAsync(u => u.Name!.Contains("Ali"));

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task Query_Comparison_ShouldWork()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var found = await _users.FindAsync(u => u.Age >= 30);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task Query_ComplexExpression_ShouldWork()
    {
        await _users.InsertManyAsync(new[]
        {
            new TestUser { Name = "Alice", Age = 30, Email = "alice@example.com" },
            new TestUser { Name = "Bob", Age = 25, Email = "bob@example.com" },
            new TestUser { Name = "Carol", Age = 35, Email = "carol@other.com" }
        });

        var found = await _users.FindAsync(u => u.Age >= 30 && u.Email!.Contains("example"));

        Assert.Single(found);
        Assert.Equal("Alice", found[0].Name);
    }

    private class TestUser
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
