using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Codezerg.DocumentStore.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codezerg.DocumentStore.Benchmarks;

/// <summary>
/// Benchmarks for DocumentStore serialization and database operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class JsonbSerializationBenchmarks
{
    private SqliteConnection? _connection;
    private User _smallDoc = null!;
    private Order _mediumDoc = null!;
    private BlogPost _largeDoc = null!;

    public class User
    {
        public DocumentId Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public int Age { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Order
    {
        public DocumentId Id { get; set; }
        public string OrderNumber { get; set; } = "";
        public Customer Customer { get; set; } = new();
        public List<OrderItem> Items { get; set; } = new();
        public Address ShippingAddress { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class Customer
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class OrderItem
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string ZipCode { get; set; } = "";
    }

    public class BlogPost
    {
        public DocumentId Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public Author Author { get; set; } = new();
        public List<Comment> Comments { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class Author
    {
        public string Name { get; set; } = "";
        public string Bio { get; set; } = "";
    }

    public class Comment
    {
        public string AuthorName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        // Create test documents
        _smallDoc = new User
        {
            Id = DocumentId.NewId(),
            Name = "John Doe",
            Email = "john@example.com",
            Age = 30,
            CreatedAt = DateTime.UtcNow
        };

        _mediumDoc = new Order
        {
            Id = DocumentId.NewId(),
            OrderNumber = "ORD-12345",
            Customer = new Customer { Name = "Jane Smith", Email = "jane@example.com" },
            Items = Enumerable.Range(1, 10).Select(i => new OrderItem
            {
                Sku = $"SKU-{i}",
                Name = $"Product {i}",
                Quantity = i,
                Price = 9.99m * i
            }).ToList(),
            ShippingAddress = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                State = "IL",
                ZipCode = "62701"
            },
            CreatedAt = DateTime.UtcNow
        };

        _largeDoc = new BlogPost
        {
            Id = DocumentId.NewId(),
            Title = "Understanding SQLite JSONB Format",
            Content = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", 100)),
            Author = new Author
            {
                Name = "Tech Blogger",
                Bio = "Passionate about databases and performance optimization"
            },
            Comments = Enumerable.Range(1, 50).Select(i => new Comment
            {
                AuthorName = $"Commenter {i}",
                Text = $"This is comment number {i} with some additional text to make it realistic.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            }).ToList(),
            Tags = new List<string> { "sqlite", "jsonb", "performance", "database", "optimization" },
            CreatedAt = DateTime.UtcNow
        };

        // Setup SQLite connection
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Create test table
        _connection.Execute(@"
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY,
                data BLOB NOT NULL
            );
        ");

        // Pre-insert documents for read benchmarks
        var smallJson = DocumentSerializer.Serialize(_smallDoc);
        _connection.Execute("INSERT INTO documents (id, data) VALUES (100, jsonb(@json))", new { json = smallJson });

        var mediumJson = DocumentSerializer.Serialize(_mediumDoc);
        _connection.Execute("INSERT INTO documents (id, data) VALUES (200, jsonb(@json))", new { json = mediumJson });

        var largeJson = DocumentSerializer.Serialize(_largeDoc);
        _connection.Execute("INSERT INTO documents (id, data) VALUES (300, jsonb(@json))", new { json = largeJson });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // ============================================
    // Serialization Benchmarks
    // ============================================

    [Benchmark(Description = "Small: Serialize to JSON")]
    public string SmallDoc_Serialize()
    {
        return DocumentSerializer.Serialize(_smallDoc);
    }

    [Benchmark(Description = "Medium: Serialize to JSON")]
    public string MediumDoc_Serialize()
    {
        return DocumentSerializer.Serialize(_mediumDoc);
    }

    [Benchmark(Description = "Large: Serialize to JSON")]
    public string LargeDoc_Serialize()
    {
        return DocumentSerializer.Serialize(_largeDoc);
    }

    // ============================================
    // Insert Benchmarks
    // ============================================

    [Benchmark(Description = "Small: Full insert with jsonb()")]
    public void SmallDoc_Insert()
    {
        var json = DocumentSerializer.Serialize(_smallDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (1, jsonb(@json)); DELETE FROM documents WHERE id = 1;",
            new { json });
    }

    [Benchmark(Description = "Medium: Full insert with jsonb()")]
    public void MediumDoc_Insert()
    {
        var json = DocumentSerializer.Serialize(_mediumDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (2, jsonb(@json)); DELETE FROM documents WHERE id = 2;",
            new { json });
    }

    [Benchmark(Description = "Large: Full insert with jsonb()")]
    public void LargeDoc_Insert()
    {
        var json = DocumentSerializer.Serialize(_largeDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (3, jsonb(@json)); DELETE FROM documents WHERE id = 3;",
            new { json });
    }

    // ============================================
    // Read Benchmarks
    // ============================================

    [Benchmark(Description = "Small: Read and deserialize")]
    public User SmallDoc_Read()
    {
        var json = _connection!.ExecuteScalar<string>("SELECT json(data) FROM documents WHERE id = 100");
        return DocumentSerializer.Deserialize<User>(json)!;
    }

    [Benchmark(Description = "Medium: Read and deserialize")]
    public Order MediumDoc_Read()
    {
        var json = _connection!.ExecuteScalar<string>("SELECT json(data) FROM documents WHERE id = 200");
        return DocumentSerializer.Deserialize<Order>(json)!;
    }

    [Benchmark(Description = "Large: Read and deserialize")]
    public BlogPost LargeDoc_Read()
    {
        var json = _connection!.ExecuteScalar<string>("SELECT json(data) FROM documents WHERE id = 300");
        return DocumentSerializer.Deserialize<BlogPost>(json)!;
    }

    // ============================================
    // Round-trip Benchmarks
    // ============================================

    [Benchmark(Description = "Round-trip: Serialize and deserialize")]
    public User RoundTrip()
    {
        var json = DocumentSerializer.Serialize(_smallDoc);
        return DocumentSerializer.Deserialize<User>(json)!;
    }
}
