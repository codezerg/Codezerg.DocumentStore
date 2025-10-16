# BinaryDocumentSerializer Usage Guide

## ⚠️ Important Warning

**The BinaryDocumentSerializer is EXPERIMENTAL and NOT RECOMMENDED for production use.**

### Why Not Use It?

1. **SQLite's Official Position**:
   > "JSONB is not intended as an external format to be used by applications. JSONB is designed for internal use by SQLite only."
   > — https://sqlite.org/jsonb.html

2. **Format Instability**: The JSONB format may change in future SQLite versions without notice.

3. **Uncertain Performance Benefit**: The `jsonb()` conversion overhead is typically minimal (~20-50μs per document).

4. **Added Complexity**: ~500 lines of binary encoding code vs 1 line with `jsonb()`.

### Recommended Approach

**Use DocumentSerializer (JSON text) + SQLite's `jsonb()` function:**

```csharp
// RECOMMENDED: Let SQLite handle the conversion
var json = DocumentSerializer.Serialize(document);
connection.Execute("INSERT INTO documents (data) VALUES (jsonb(@json))", new { json });
```

This provides:
- ✅ Official SQLite support
- ✅ Format stability guarantee
- ✅ Simple, maintainable code
- ✅ 50%+ performance improvement over JSON text storage

## When Might You Use BinaryDocumentSerializer?

Consider using BinaryDocumentSerializer only if:

1. ✅ Benchmarks prove `jsonb()` conversion is a significant bottleneck (>30% of total time)
2. ✅ You're willing to accept format stability risks
3. ✅ You have comprehensive tests validating output against SQLite's parser
4. ✅ You can maintain compatibility with SQLite version updates

## Usage Examples

### Basic Serialization

```csharp
using Codezerg.DocumentStore.Serialization;

// Serialize to JSONB binary blob
var document = new User { Id = DocumentId.NewId(), Name = "John Doe", Age = 30 };
byte[] jsonbBlob = BinaryDocumentSerializer.SerializeToJsonb(document);

// Deserialize from JSONB binary blob
var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<User>(jsonbBlob);
```

### Direct Database Storage

```csharp
using Dapper;
using Microsoft.Data.Sqlite;

// Create connection
using var connection = new SqliteConnection("Data Source=myapp.db");
connection.Open();

// Create table with BLOB column
connection.Execute(@"
    CREATE TABLE IF NOT EXISTS documents (
        id TEXT PRIMARY KEY,
        data BLOB NOT NULL
    );
");

// Serialize and insert
var document = new User { Id = DocumentId.NewId(), Name = "Jane Smith", Age = 25 };
var jsonbBlob = BinaryDocumentSerializer.SerializeToJsonb(document);

connection.Execute(
    "INSERT INTO documents (id, data) VALUES (@id, @data)",
    new { id = document.Id.ToString(), data = jsonbBlob });

// Retrieve and deserialize
var retrieved = connection.QuerySingle<byte[]>(
    "SELECT data FROM documents WHERE id = @id",
    new { id = document.Id.ToString() });

var deserialized = BinaryDocumentSerializer.DeserializeFromJsonb<User>(retrieved);
```

### Integration with SqliteDocumentCollection (Custom Implementation)

**Note**: This requires modifying SqliteDocumentCollection to support pluggable serializers.

```csharp
// Hypothetical custom implementation (NOT in current library)
public class BinaryDocumentCollection<T> : IDocumentCollection<T> where T : class
{
    private readonly SqliteConnection _connection;
    private readonly long _collectionId;

    public void InsertOne(T document)
    {
        var id = GetDocumentId(document) ?? DocumentId.NewId();
        SetDocumentId(document, id);

        // Use BinaryDocumentSerializer instead of DocumentSerializer
        var jsonb = BinaryDocumentSerializer.SerializeToJsonb(document);

        var sql = @"
            INSERT INTO documents (collection_id, document_id, data, created_at, updated_at)
            VALUES (@CollectionId, @DocumentId, @Data, @CreatedAt, @UpdatedAt);";

        _connection.Execute(sql, new
        {
            CollectionId = _collectionId,
            DocumentId = id.ToString(),
            Data = jsonb,  // Direct JSONB blob
            CreatedAt = DateTime.UtcNow.ToString("O"),
            UpdatedAt = DateTime.UtcNow.ToString("O")
        });
    }

    public T? FindById(DocumentId id)
    {
        var sql = "SELECT data FROM documents WHERE collection_id = @CollectionId AND document_id = @DocumentId;";

        var jsonb = _connection.QuerySingleOrDefault<byte[]>(sql, new
        {
            CollectionId = _collectionId,
            DocumentId = id.ToString()
        });

        return jsonb != null
            ? BinaryDocumentSerializer.DeserializeFromJsonb<T>(jsonb)
            : null;
    }
}
```

## Performance Comparison

Run benchmarks to measure actual performance:

```bash
cd benchmarks/Codezerg.DocumentStore.Benchmarks
dotnet run -c Release
```

### Expected Results

Based on implementation analysis:

| Operation | JSON + jsonb() | Direct JSONB Binary | Difference |
|-----------|---------------|---------------------|------------|
| Serialize small doc | ~100μs | ~110μs | Slower (more complex code path) |
| Serialize medium doc | ~500μs | ~520μs | Comparable |
| Serialize large doc | ~5ms | ~5.2ms | Comparable |
| Full insert operation | ~1ms | ~1ms | Comparable |

**Conclusion**: Direct JSONB serialization is unlikely to provide significant performance benefits.

The bottlenecks are typically:
- Disk I/O (dominates for inserts)
- Network latency (for remote databases)
- Query complexity (for reads)

## Validation Against SQLite

To validate BinaryDocumentSerializer output matches SQLite's JSONB format:

```csharp
using Microsoft.Data.Sqlite;
using System.Linq;

// Serialize with BinaryDocumentSerializer
var doc = new User { Id = DocumentId.NewId(), Name = "Test", Age = 42 };
var ourJsonb = BinaryDocumentSerializer.SerializeToJsonb(doc);

// Serialize with SQLite's jsonb()
var json = DocumentSerializer.Serialize(doc);
using var conn = new SqliteConnection("Data Source=:memory:");
conn.Open();
var sqliteJsonb = conn.ExecuteScalar<byte[]>("SELECT jsonb(@json)", new { json });

// Compare
bool identical = ourJsonb.SequenceEqual(sqliteJsonb);

if (!identical)
{
    Console.WriteLine("⚠️ WARNING: Output differs from SQLite's jsonb() function!");
    Console.WriteLine($"Our size: {ourJsonb.Length}, SQLite size: {sqliteJsonb.Length}");

    // Log differences for debugging
    for (int i = 0; i < Math.Min(ourJsonb.Length, sqliteJsonb.Length); i++)
    {
        if (ourJsonb[i] != sqliteJsonb[i])
        {
            Console.WriteLine($"Difference at byte {i}: ours=0x{ourJsonb[i]:X2}, sqlite=0x{sqliteJsonb[i]:X2}");
        }
    }
}
else
{
    Console.WriteLine("✅ Output matches SQLite's jsonb() function");
}
```

## Testing

Comprehensive tests are in `Codezerg.DocumentStore.Tests/Serialization/BinaryDocumentSerializerTests.cs`:

```bash
dotnet test --filter FullyQualifiedName~BinaryDocumentSerializerTests
```

Tests cover:
- ✅ Basic types (null, bool, numbers, strings)
- ✅ Arrays and objects
- ✅ Nested structures
- ✅ Round-trip serialization
- ✅ Edge cases (empty strings, special characters, Unicode)
- ✅ Large documents
- ✅ Size comparison with JSON text

## Limitations

1. **No SQLite Version Tracking**: BinaryDocumentSerializer doesn't track which SQLite version's format it implements.

2. **Deserialization Requires Re-parsing**: The deserializer converts JSONB → JSON text → C# object, so it's not faster than using SQLite's `json()` function.

3. **No Validation**: SQLite's `jsonb()` function includes validation and error handling that BinaryDocumentSerializer may not replicate.

4. **Limited Type Support**: Only supports JSON-compatible types. No native support for binary data, dates (stored as strings), or custom types.

5. **Maintenance Burden**: Must be updated if SQLite changes JSONB format in future versions.

## Decision Matrix

| Criterion | DocumentSerializer + jsonb() | BinaryDocumentSerializer |
|-----------|------------------------------|-------------------------|
| **Performance** | ✅ Fast (SQLite optimized C code) | ⚠️ Similar or slower (C# code) |
| **Maintainability** | ✅ 1 line of code | ❌ 500+ lines to maintain |
| **Reliability** | ✅ Official SQLite guarantee | ⚠️ Experimental, no guarantees |
| **Format Stability** | ✅ Guaranteed by SQLite | ❌ May break with SQLite updates |
| **Complexity** | ✅ Very simple | ❌ Complex binary encoding |
| **Testing** | ✅ Tested by SQLite team | ⚠️ Requires extensive custom tests |
| **Risk** | ✅ Very low | ⚠️ High |

## Recommendation

**Use DocumentSerializer + `jsonb()` unless:**
1. Benchmarks prove >30% of time is spent in `jsonb()` conversion
2. You have specific requirements that justify the risks
3. You can maintain compatibility with SQLite version updates

## References

- **SQLite JSONB Documentation**: https://sqlite.org/jsonb.html
- **JSONB Format Specification**: https://github.com/sqlite/sqlite/blob/master/doc/jsonb.md
- **BinaryDocumentSerializer Source**: `src/Codezerg.DocumentStore/Serialization/BinaryDocumentSerializer.cs`
- **Benchmark Project**: `benchmarks/Codezerg.DocumentStore.Benchmarks/`
- **Performance Analysis**: `docs/JSONB_SERIALIZATION_ANALYSIS.md`

---

**Status**: ⚠️ Experimental - Use at Your Own Risk
**Last Updated**: 2025-10-16
**Recommended Approach**: DocumentSerializer + SQLite `jsonb()` function
