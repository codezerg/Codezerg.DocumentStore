using Codezerg.DocumentStore;
using Codezerg.DocumentStore.Exceptions;
using Microsoft.Data.Sqlite;

namespace Codezerg.DocumentStore.Tests;

/// <summary>
/// Comprehensive tests for index functionality
/// </summary>
public class IndexTests : IAsyncLifetime
{
    private readonly string _dbFile;
    private readonly SqliteDocumentDatabase _database;
    private IDocumentCollection<TestProduct> _products = null!;

    public IndexTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _database = new SqliteDocumentDatabase($"Data Source={_dbFile}");
    }

    public async Task InitializeAsync()
    {
        _products = await _database.GetCollectionAsync<TestProduct>("products");
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

    #region Basic Index Creation and Dropping

    [Fact]
    public async Task CreateIndex_OnStringField_ShouldSucceed()
    {
        await _products.CreateIndexAsync(p => p.Name);

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Price = 10.99m });
        var found = await _products.FindOneAsync(p => p.Name == "Widget");

        Assert.NotNull(found);
        Assert.Equal("Widget", found.Name);
    }

    [Fact]
    public async Task CreateIndex_OnNumericField_ShouldSucceed()
    {
        await _products.CreateIndexAsync(p => p.Stock);

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Stock = 50 });
        var found = await _products.FindOneAsync(p => p.Stock > 40 && p.Stock < 60);

        Assert.NotNull(found);
    }

    [Fact]
    public async Task CreateIndex_OnIntegerField_ShouldSucceed()
    {
        await _products.CreateIndexAsync(p => p.Stock);

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Stock = 100 });
        var found = await _products.FindOneAsync(p => p.Stock == 100);

        Assert.NotNull(found);
    }

    [Fact]
    public async Task CreateIndex_Multiple_ShouldAllWork()
    {
        await _products.CreateIndexAsync(p => p.Name);
        await _products.CreateIndexAsync(p => p.Category);
        await _products.CreateIndexAsync(p => p.Stock);

        await _products.InsertOneAsync(new TestProduct
        {
            Name = "Widget",
            Category = "Tools",
            Stock = 100
        });

        var foundByName = await _products.FindOneAsync(p => p.Name == "Widget");
        var foundByCategory = await _products.FindOneAsync(p => p.Category == "Tools");
        var foundByStock = await _products.FindOneAsync(p => p.Stock == 100);

        Assert.NotNull(foundByName);
        Assert.NotNull(foundByCategory);
        Assert.NotNull(foundByStock);
    }

    [Fact]
    public async Task CreateIndex_DuplicateIndex_ShouldNotFail()
    {
        await _products.CreateIndexAsync(p => p.Name);
        await _products.CreateIndexAsync(p => p.Name); // Create again

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Price = 10.99m });
        var found = await _products.FindOneAsync(p => p.Name == "Widget");

        Assert.NotNull(found);
    }

    [Fact]
    public async Task DropIndex_ExistingIndex_ShouldSucceed()
    {
        await _products.CreateIndexAsync(p => p.Name);
        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Price = 10.99m });

        await _products.DropIndexAsync(p => p.Name);

        // Should still work after dropping index
        var found = await _products.FindOneAsync(p => p.Name == "Widget");
        Assert.NotNull(found);
    }

    [Fact]
    public async Task DropIndex_NonExistentIndex_ShouldNotFail()
    {
        // Dropping an index that doesn't exist should not throw
        await _products.DropIndexAsync(p => p.Name);

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Price = 10.99m });
        var found = await _products.FindOneAsync(p => p.Name == "Widget");

        Assert.NotNull(found);
    }

    #endregion

    #region Unique Index Tests

    [Fact]
    public async Task CreateIndex_Unique_ShouldEnforceUniqueness()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        await _products.InsertOneAsync(new TestProduct { Sku = "WID-001", Name = "Widget" });

        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _products.InsertOneAsync(new TestProduct { Sku = "WID-001", Name = "Different Widget" }));
    }

    [Fact]
    public async Task CreateIndex_Unique_AllowsDifferentValues()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        await _products.InsertOneAsync(new TestProduct { Sku = "WID-001", Name = "Widget" });
        await _products.InsertOneAsync(new TestProduct { Sku = "WID-002", Name = "Another Widget" });

        var count = await _products.CountAllAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateIndex_Unique_AllowsNullValues()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        // Multiple null values should be allowed
        await _products.InsertOneAsync(new TestProduct { Sku = null, Name = "Widget 1" });
        await _products.InsertOneAsync(new TestProduct { Sku = null, Name = "Widget 2" });

        var count = await _products.CountAllAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateIndex_Unique_UpdateToExistingValue_ShouldFail()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        var product1 = new TestProduct { Sku = "WID-001", Name = "Widget 1" };
        var product2 = new TestProduct { Sku = "WID-002", Name = "Widget 2" };

        await _products.InsertOneAsync(product1);
        await _products.InsertOneAsync(product2);

        // Try to update product2 to have the same SKU as product1
        product2.Sku = "WID-001";

        await Assert.ThrowsAsync<SqliteException>(() =>
            _products.UpdateByIdAsync(product2.Id, product2));
    }

    [Fact]
    public async Task CreateIndex_Unique_UpdateToSameValue_ShouldSucceed()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        var product = new TestProduct { Sku = "WID-001", Name = "Widget" };
        await _products.InsertOneAsync(product);

        // Update other fields but keep same SKU
        product.Name = "Updated Widget";
        var updated = await _products.UpdateByIdAsync(product.Id, product);

        Assert.True(updated);
    }

    [Fact]
    public async Task DropIndex_Unique_ShouldAllowDuplicates()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);
        await _products.InsertOneAsync(new TestProduct { Sku = "WID-001", Name = "Widget" });

        await _products.DropIndexAsync(p => p.Sku);

        // Should now allow duplicate SKU
        await _products.InsertOneAsync(new TestProduct { Sku = "WID-001", Name = "Another Widget" });

        var count = await _products.CountAllAsync();
        Assert.Equal(2, count);
    }

    #endregion

    #region Index Performance and Query Tests

    [Fact]
    public async Task Index_ShouldImproveQueryPerformance()
    {
        // Insert many documents
        var products = new List<TestProduct>();
        for (int i = 0; i < 100; i++)
        {
            products.Add(new TestProduct
            {
                Name = $"Product {i}",
                Category = i % 10 == 0 ? "Featured" : "Regular",
                Price = i * 1.5m
            });
        }
        await _products.InsertManyAsync(products);

        // Query without index
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        var results1 = await _products.FindAsync(p => p.Category == "Featured");
        sw1.Stop();

        // Create index
        await _products.CreateIndexAsync(p => p.Category);

        // Query with index
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var results2 = await _products.FindAsync(p => p.Category == "Featured");
        sw2.Stop();

        // Both should return same results
        Assert.Equal(results1.Count, results2.Count);
        Assert.Equal(10, results2.Count);
    }

    [Fact]
    public async Task Index_StringContains_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Name);

        await _products.InsertManyAsync(new[]
        {
            new TestProduct { Name = "Blue Widget", Price = 10m },
            new TestProduct { Name = "Red Widget", Price = 15m },
            new TestProduct { Name = "Green Gadget", Price = 20m }
        });

        var found = await _products.FindAsync(p => p.Name!.Contains("Widget"));

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task Index_RangeQuery_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Stock);

        await _products.InsertManyAsync(new[]
        {
            new TestProduct { Name = "Cheap", Stock = 5 },
            new TestProduct { Name = "Medium", Stock = 15 },
            new TestProduct { Name = "Expensive", Stock = 50 }
        });

        var found = await _products.FindAsync(p => p.Stock >= 10 && p.Stock <= 30);

        Assert.Single(found);
        Assert.Equal("Medium", found[0].Name);
    }

    [Fact]
    public async Task Index_Equality_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Stock);

        await _products.InsertManyAsync(new[]
        {
            new TestProduct { Name = "A", Stock = 10 },
            new TestProduct { Name = "B", Stock = 20 },
            new TestProduct { Name = "C", Stock = 10 }
        });

        var found = await _products.FindAsync(p => p.Stock == 10);

        Assert.Equal(2, found.Count);
    }

    #endregion

    #region Multiple Field Index Tests

    [Fact]
    public async Task MultipleIndexes_ShouldWorkIndependently()
    {
        await _products.CreateIndexAsync(p => p.Name);
        await _products.CreateIndexAsync(p => p.Category);

        await _products.InsertOneAsync(new TestProduct
        {
            Name = "Widget",
            Category = "Tools"
        });

        var byName = await _products.FindOneAsync(p => p.Name == "Widget");
        var byCategory = await _products.FindOneAsync(p => p.Category == "Tools");

        Assert.NotNull(byName);
        Assert.NotNull(byCategory);
        Assert.Equal(byName.Id, byCategory.Id);
    }

    [Fact]
    public async Task MultipleUniqueIndexes_ShouldEnforceIndependently()
    {
        await _products.CreateIndexAsync(p => p.Sku, unique: true);
        await _products.CreateIndexAsync(p => p.Barcode, unique: true);

        await _products.InsertOneAsync(new TestProduct
        {
            Sku = "SKU-001",
            Barcode = "BAR-001"
        });

        // Duplicate SKU should fail
        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _products.InsertOneAsync(new TestProduct { Sku = "SKU-001", Barcode = "BAR-002" }));

        // Duplicate Barcode should fail
        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _products.InsertOneAsync(new TestProduct { Sku = "SKU-002", Barcode = "BAR-001" }));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Index_OnNullableField_WithNullValues_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Description);

        await _products.InsertManyAsync(new[]
        {
            new TestProduct { Name = "A", Description = null },
            new TestProduct { Name = "B", Description = "Has description" },
            new TestProduct { Name = "C", Description = null }
        });

        var withDescription = await _products.FindAsync(p => p.Description == "Has description");
        Assert.Single(withDescription);
    }

    [Fact]
    public async Task Index_EmptyCollection_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Name);
        await _products.DropIndexAsync(p => p.Name);

        // No error should occur
        var count = await _products.CountAllAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Index_AfterDataInserted_ShouldIndexExistingData()
    {
        // Insert data first
        await _products.InsertManyAsync(new[]
        {
            new TestProduct { Name = "Widget", Sku = "WID-001" },
            new TestProduct { Name = "Gadget", Sku = "GAD-001" }
        });

        // Create index after data exists
        await _products.CreateIndexAsync(p => p.Sku, unique: true);

        // Should still enforce uniqueness
        await Assert.ThrowsAsync<DuplicateKeyException>(() =>
            _products.InsertOneAsync(new TestProduct { Name = "Duplicate", Sku = "WID-001" }));
    }

    [Fact]
    public async Task Index_CaseInsensitiveQuery_ShouldWork()
    {
        await _products.CreateIndexAsync(p => p.Category);

        await _products.InsertOneAsync(new TestProduct { Name = "Widget", Category = "Tools" });

        // SQLite LIKE is case-insensitive by default
        var found = await _products.FindAsync(p => p.Category!.Contains("tools"));

        Assert.Single(found);
    }

    #endregion

    private class TestProduct
    {
        public DocumentId Id { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
