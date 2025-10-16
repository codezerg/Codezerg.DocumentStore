using Codezerg.DocumentStore;
using Codezerg.DocumentStore.Exceptions;

namespace Codezerg.DocumentStore.Tests;

public class SqliteDocumentCollectionTests : IDisposable
{
    private readonly SqliteDocumentDatabase _database;
    private readonly IDocumentCollection<TestUser> _users;

    public SqliteDocumentCollectionTests()
    {
        _database = SqliteDocumentDatabase.CreateInMemory();
        _users = _database.GetCollection<TestUser>("users");
    }

    public void Dispose()
    {
        _database?.Dispose();
    }

    [Fact]
    public void InsertOne_ShouldInsertDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };

        _users.InsertOne(user);

        Assert.NotEqual(DocumentId.Empty, user.Id);
        Assert.NotEqual(default, user.CreatedAt);
        Assert.NotEqual(default, user.UpdatedAt);
    }

    [Fact]
    public void InsertOne_ShouldThrowOnDuplicateId()
    {
        var user1 = new TestUser { Name = "Alice", Age = 30 };
        _users.InsertOne(user1);

        var user2 = new TestUser { Id = user1.Id, Name = "Bob", Age = 25 };

        Assert.Throws<DuplicateKeyException>(() => _users.InsertOne(user2));
    }

    [Fact]
    public void InsertMany_ShouldInsertMultipleDocuments()
    {
        var users = new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        };

        _users.InsertMany(users);

        var count = _users.CountAll();
        Assert.Equal(3, count);
    }

    [Fact]
    public void FindById_ShouldReturnDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        _users.InsertOne(user);

        var found = _users.FindById(user.Id);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.Equal("Alice", found.Name);
        Assert.Equal(30, found.Age);
    }

    [Fact]
    public void FindById_ShouldReturnNullForNonExistent()
    {
        var found = _users.FindById(DocumentId.NewId());

        Assert.Null(found);
    }

    [Fact]
    public void FindOne_ShouldReturnFirstMatch()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var found = _users.FindOne(u => u.Age == 30);

        Assert.NotNull(found);
        Assert.Equal(30, found.Age);
    }

    [Fact]
    public void FindOne_ShouldReturnNullWhenNoMatch()
    {
        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });

        var found = _users.FindOne(u => u.Age == 99);

        Assert.Null(found);
    }

    [Fact]
    public void Find_ShouldReturnMatchingDocuments()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var found = _users.Find(u => u.Age == 30);

        Assert.Equal(2, found.Count);
        Assert.All(found, u => Assert.Equal(30, u.Age));
    }

    [Fact]
    public void Find_WithPagination_ShouldReturnCorrectPage()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "User1", Age = 20 },
            new TestUser { Name = "User2", Age = 20 },
            new TestUser { Name = "User3", Age = 20 },
            new TestUser { Name = "User4", Age = 20 },
            new TestUser { Name = "User5", Age = 20 }
        });

        var page1 = _users.Find(u => u.Age == 20, skip: 0, limit: 2);
        var page2 = _users.Find(u => u.Age == 20, skip: 2, limit: 2);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public void FindAll_ShouldReturnAllDocuments()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var all = _users.FindAll();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void Count_ShouldReturnMatchingCount()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 30 }
        });

        var count = _users.Count(u => u.Age == 30);

        Assert.Equal(2, count);
    }

    [Fact]
    public void CountAll_ShouldReturnTotalCount()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var count = _users.CountAll();

        Assert.Equal(3, count);
    }

    [Fact]
    public void UpdateById_ShouldUpdateDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        _users.InsertOne(user);

        user.Name = "Alice Updated";
        user.Age = 31;
        var updated = _users.UpdateById(user.Id, user);

        Assert.True(updated);

        var found = _users.FindById(user.Id);
        Assert.NotNull(found);
        Assert.Equal("Alice Updated", found.Name);
        Assert.Equal(31, found.Age);
    }

    [Fact]
    public void UpdateById_ShouldReturnFalseForNonExistent()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };

        var updated = _users.UpdateById(DocumentId.NewId(), user);

        Assert.False(updated);
    }

    [Fact]
    public void UpdateOne_ShouldUpdateFirstMatch()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 }
        });

        var newUser = new TestUser { Name = "Updated", Age = 31 };
        var updated = _users.UpdateOne(u => u.Age == 30, newUser);

        Assert.True(updated);

        var count = _users.Count(u => u.Age == 31);
        Assert.Equal(1, count);
    }

    [Fact]
    public void UpdateMany_ShouldUpdateAllMatches()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 },
            new TestUser { Name = "Carol", Age = 25 }
        });

        var updateCount = _users.UpdateMany(u => u.Age == 30, user => user.Age = 31);

        Assert.Equal(2, updateCount);

        var count = _users.Count(u => u.Age == 31);
        Assert.Equal(2, count);
    }

    [Fact]
    public void DeleteById_ShouldDeleteDocument()
    {
        var user = new TestUser { Name = "Alice", Age = 30 };
        _users.InsertOne(user);

        var deleted = _users.DeleteById(user.Id);

        Assert.True(deleted);

        var found = _users.FindById(user.Id);
        Assert.Null(found);
    }

    [Fact]
    public void DeleteById_ShouldReturnFalseForNonExistent()
    {
        var deleted = _users.DeleteById(DocumentId.NewId());

        Assert.False(deleted);
    }

    [Fact]
    public void DeleteOne_ShouldDeleteFirstMatch()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 }
        });

        var deleted = _users.DeleteOne(u => u.Age == 30);

        Assert.True(deleted);

        var count = _users.CountAll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void DeleteMany_ShouldDeleteAllMatches()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 30 },
            new TestUser { Name = "Carol", Age = 25 }
        });

        var deleteCount = _users.DeleteMany(u => u.Age == 30);

        Assert.Equal(2, deleteCount);

        var count = _users.CountAll();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Any_ShouldReturnTrueWhenMatches()
    {
        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });

        var exists = _users.Any(u => u.Age == 30);

        Assert.True(exists);
    }

    [Fact]
    public void Any_ShouldReturnFalseWhenNoMatches()
    {
        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });

        var exists = _users.Any(u => u.Age == 99);

        Assert.False(exists);
    }

    [Fact]
    public void CreateIndex_ShouldCreateIndex()
    {
        _users.CreateIndex(u => u.Name);

        // Insert and query to verify index works
        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });
        var found = _users.FindOne(u => u.Name == "Alice");

        Assert.NotNull(found);
    }

    [Fact]
    public void CreateIndex_Unique_ShouldEnforceUniqueness()
    {
        _users.CreateIndex(u => u.Name, unique: true);

        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });

        // This should fail due to unique constraint
        Assert.Throws<DuplicateKeyException>(() =>
            _users.InsertOne(new TestUser { Name = "Alice", Age = 25 }));
    }

    [Fact]
    public void DropIndex_ShouldDropIndex()
    {
        _users.CreateIndex(u => u.Name);
        _users.DropIndex(u => u.Name);

        // Should still work without index
        _users.InsertOne(new TestUser { Name = "Alice", Age = 30 });
        var found = _users.FindOne(u => u.Name == "Alice");

        Assert.NotNull(found);
    }

    [Fact]
    public void Query_StringContains_ShouldWork()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Alicia", Age = 35 }
        });

        var found = _users.Find(u => u.Name!.Contains("Ali"));

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Query_Comparison_ShouldWork()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30 },
            new TestUser { Name = "Bob", Age = 25 },
            new TestUser { Name = "Carol", Age = 35 }
        });

        var found = _users.Find(u => u.Age >= 30);

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Query_ComplexExpression_ShouldWork()
    {
        _users.InsertMany(new[]
        {
            new TestUser { Name = "Alice", Age = 30, Email = "alice@example.com" },
            new TestUser { Name = "Bob", Age = 25, Email = "bob@example.com" },
            new TestUser { Name = "Carol", Age = 35, Email = "carol@other.com" }
        });

        var found = _users.Find(u => u.Age >= 30 && u.Email!.Contains("example"));

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
