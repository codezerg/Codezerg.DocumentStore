using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Codezerg.DocumentStore.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codezerg.DocumentStore.Benchmarks;

/// <summary>
/// Benchmarks comparing JSON text serialization + SQLite jsonb() conversion
/// versus direct JSONB binary serialization.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
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
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
    }

    // ============================================
    // Small Document Benchmarks (~500 bytes)
    // ============================================

    [Benchmark(Description = "Small: JSON text serialization only")]
    public string SmallDoc_JsonText()
    {
        return DocumentSerializer.Serialize(_smallDoc);
    }

    [Benchmark(Description = "Small: JSONB binary serialization only")]
    public byte[] SmallDoc_JsonbBinary()
    {
        return BinaryDocumentSerializer.SerializeToJsonb(_smallDoc);
    }

    [Benchmark(Description = "Small: JSON + SQLite jsonb() conversion")]
    public void SmallDoc_JsonWithSqliteConversion()
    {
        var json = DocumentSerializer.Serialize(_smallDoc);
        _connection!.ExecuteScalar<byte[]>("SELECT jsonb(@json)", new { json });
    }

    [Benchmark(Description = "Small: Full insert with JSON + jsonb()")]
    public void SmallDoc_InsertWithJsonbFunction()
    {
        var json = DocumentSerializer.Serialize(_smallDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (1, jsonb(@json)); DELETE FROM documents WHERE id = 1;",
            new { json });
    }

    [Benchmark(Description = "Small: Full insert with JSONB binary")]
    public void SmallDoc_InsertWithJsonbBinary()
    {
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(_smallDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (1, @jsonb); DELETE FROM documents WHERE id = 1;",
            new { jsonb });
    }

    // ============================================
    // Medium Document Benchmarks (~2-5KB)
    // ============================================

    [Benchmark(Description = "Medium: JSON text serialization only")]
    public string MediumDoc_JsonText()
    {
        return DocumentSerializer.Serialize(_mediumDoc);
    }

    [Benchmark(Description = "Medium: JSONB binary serialization only")]
    public byte[] MediumDoc_JsonbBinary()
    {
        return BinaryDocumentSerializer.SerializeToJsonb(_mediumDoc);
    }

    [Benchmark(Description = "Medium: JSON + SQLite jsonb() conversion")]
    public void MediumDoc_JsonWithSqliteConversion()
    {
        var json = DocumentSerializer.Serialize(_mediumDoc);
        _connection!.ExecuteScalar<byte[]>("SELECT jsonb(@json)", new { json });
    }

    [Benchmark(Description = "Medium: Full insert with JSON + jsonb()")]
    public void MediumDoc_InsertWithJsonbFunction()
    {
        var json = DocumentSerializer.Serialize(_mediumDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (2, jsonb(@json)); DELETE FROM documents WHERE id = 2;",
            new { json });
    }

    [Benchmark(Description = "Medium: Full insert with JSONB binary")]
    public void MediumDoc_InsertWithJsonbBinary()
    {
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(_mediumDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (2, @jsonb); DELETE FROM documents WHERE id = 2;",
            new { jsonb });
    }

    // ============================================
    // Large Document Benchmarks (~10-50KB)
    // ============================================

    [Benchmark(Description = "Large: JSON text serialization only")]
    public string LargeDoc_JsonText()
    {
        return DocumentSerializer.Serialize(_largeDoc);
    }

    [Benchmark(Description = "Large: JSONB binary serialization only")]
    public byte[] LargeDoc_JsonbBinary()
    {
        return BinaryDocumentSerializer.SerializeToJsonb(_largeDoc);
    }

    [Benchmark(Description = "Large: JSON + SQLite jsonb() conversion")]
    public void LargeDoc_JsonWithSqliteConversion()
    {
        var json = DocumentSerializer.Serialize(_largeDoc);
        _connection!.ExecuteScalar<byte[]>("SELECT jsonb(@json)", new { json });
    }

    [Benchmark(Description = "Large: Full insert with JSON + jsonb()")]
    public void LargeDoc_InsertWithJsonbFunction()
    {
        var json = DocumentSerializer.Serialize(_largeDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (3, jsonb(@json)); DELETE FROM documents WHERE id = 3;",
            new { json });
    }

    [Benchmark(Description = "Large: Full insert with JSONB binary")]
    public void LargeDoc_InsertWithJsonbBinary()
    {
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(_largeDoc);
        _connection!.Execute("INSERT INTO documents (id, data) VALUES (3, @jsonb); DELETE FROM documents WHERE id = 3;",
            new { jsonb });
    }

    // ============================================
    // Round-trip Benchmarks
    // ============================================

    [Benchmark(Description = "Round-trip: JSON text (serialize + deserialize)")]
    public User RoundTrip_JsonText()
    {
        var json = DocumentSerializer.Serialize(_smallDoc);
        return DocumentSerializer.Deserialize<User>(json)!;
    }

    [Benchmark(Description = "Round-trip: JSONB binary (serialize + deserialize)")]
    public User RoundTrip_JsonbBinary()
    {
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(_smallDoc);
        return BinaryDocumentSerializer.DeserializeFromJsonb<User>(jsonb)!;
    }

    // ============================================
    // Size Comparison
    // ============================================

    [Benchmark(Description = "Size: JSON text size")]
    public int Size_JsonText()
    {
        var json = DocumentSerializer.Serialize(_mediumDoc);
        return System.Text.Encoding.UTF8.GetByteCount(json);
    }

    [Benchmark(Description = "Size: JSONB binary size")]
    public int Size_JsonbBinary()
    {
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(_mediumDoc);
        return jsonb.Length;
    }
}
