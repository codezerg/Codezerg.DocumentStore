# SQLite JSONB Reference Documentation

**Last Updated**: 2025-10-16
**SQLite Version Required**: 3.45.0 or higher (released 2024-01-15)
**Official Documentation**: https://sqlite.org/jsonb.html

---

## Overview

SQLite's JSONB is a binary encoding format for JSON data that provides significant performance improvements over text-based JSON storage. JSONB is SQLite's own format (not compatible with PostgreSQL JSONB or MongoDB BSON).

### Key Benefits

| Feature | JSON Text | JSONB Binary | Improvement |
|---------|-----------|--------------|-------------|
| Storage Size | Baseline | 5-10% smaller | ✅ |
| CPU Cycles | Baseline | **<50% of text** | ✅✅✅ |
| Query Support | Full | Full | ✅ |
| json_extract() | Supported | Supported | ✅ |
| Human Readable | Yes | No | ⚠️ |

**Source**: https://sqlite.org/jsonb.html

---

## How JSONB Works

### Binary Encoding

JSONB replaces JSON text punctuation (quotes, brackets, colons, commas) with binary headers:

- **Header**: 1-9 bytes containing element type and payload size
- **Payload**: The actual data (same as text JSON for most types)

Example:
```json
// JSON Text (26 bytes)
{"name":"John","age":30}

// JSONB (approximately 24 bytes)
[binary header][name][John][age][30]
```

### Element Types

JSONB supports 13 element types:

| Type | Description | Storage |
|------|-------------|---------|
| NULL | JSON null | Header only |
| TRUE | JSON true | Header only |
| FALSE | JSON false | Header only |
| INT | Small integer | Header only |
| INT5 | 1-5 byte integer | Header + bytes |
| FLOAT | Floating point | Header + ASCII text |
| FLOAT5 | 5-byte float | Header + 5 bytes |
| TEXT | UTF-8 string | Header + string |
| TEXTJ | JSON-escaped string | Header + string |
| TEXT5 | Large string | Header + string |
| TEXTRAW | Raw text | Header + string |
| ARRAY | JSON array | Header + elements |
| OBJECT | JSON object | Header + key-value pairs |

---

## SQL Functions

### Functions Returning JSONB (Binary)

All functions with `jsonb` prefix return JSONB blobs:

```sql
-- Convert JSON text to JSONB blob
SELECT jsonb('{"name":"John","age":30}');

-- Create JSONB from parts
SELECT jsonb_object('name', 'John', 'age', 30);

-- Create JSONB array
SELECT jsonb_array(1, 2, 3, 4, 5);

-- Insert/replace in JSONB
SELECT jsonb_insert('{"a":1}', '$.b', 2);
SELECT jsonb_replace('{"a":1}', '$.a', 2);

-- Remove from JSONB
SELECT jsonb_remove('{"a":1,"b":2}', '$.b');

-- Set value in JSONB
SELECT jsonb_set('{"a":1}', '$.b', 2);

-- Patch JSONB
SELECT jsonb_patch('{"a":1}', '{"b":2}');
```

### Functions Accepting Both Text and JSONB

All `json_` functions work with **both** text JSON and JSONB blobs:

```sql
-- Extract works with both formats
SELECT json_extract(data, '$.name') FROM documents;

-- Type checking
SELECT json_type(data) FROM documents;

-- Array/object length
SELECT json_array_length(data, '$.items') FROM documents;

-- Iterate array
SELECT value FROM json_each('{"items":[1,2,3]}', '$.items');

-- Iterate object keys
SELECT key, value FROM json_tree('{"name":"John","age":30}');
```

### Conversion Between Formats

```sql
-- JSONB to JSON text
SELECT json(jsonb_data) FROM documents;

-- JSON text to JSONB
SELECT jsonb(json_data) FROM documents;

-- Check if data is JSONB
SELECT typeof(data) FROM documents;  -- Returns 'blob' for JSONB, 'text' for JSON
```

---

## Usage in Codezerg.DocumentStore

### Current Implementation (JSON Text)

```sql
-- Schema
CREATE TABLE documents (
    id BLOB PRIMARY KEY,
    collection_id INTEGER NOT NULL,
    data TEXT NOT NULL,  -- JSON as text
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

-- Insert
INSERT INTO documents (id, collection_id, data)
VALUES (?, ?, '{"name":"John","age":30}');

-- Query
SELECT data
FROM documents
WHERE collection_id = ?
  AND json_extract(data, '$.age') > 30;
```

### Proposed Implementation (JSONB Binary)

```sql
-- Schema (change data column to BLOB)
CREATE TABLE documents (
    id BLOB PRIMARY KEY,
    collection_id INTEGER NOT NULL,
    data BLOB NOT NULL,  -- JSONB as blob
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

-- Insert (wrap with jsonb() function)
INSERT INTO documents (id, collection_id, data)
VALUES (?, ?, jsonb('{"name":"John","age":30}'));

-- Query (NO CHANGES - json_extract works with BLOB!)
SELECT data
FROM documents
WHERE collection_id = ?
  AND json_extract(data, '$.age') > 30;

-- Retrieve (convert back to text if needed)
SELECT json(data) FROM documents WHERE id = ?;
```

---

## C# Implementation Example

### Before (Current - JSON Text)

```csharp
public void Insert<T>(T document) where T : class
{
    var json = DocumentSerializer.Serialize(document);
    var sql = @"
        INSERT INTO documents (id, collection_id, data)
        VALUES (@id, @collectionId, @data)";

    _connection.Execute(sql, new {
        id = document.Id.ToByteArray(),
        collectionId = _collectionId,
        data = json  // Store as text
    });
}

public T FindById<T>(DocumentId id) where T : class
{
    var sql = "SELECT data FROM documents WHERE id = @id";
    var json = _connection.QuerySingleOrDefault<string>(sql, new { id = id.ToByteArray() });
    return DocumentSerializer.Deserialize<T>(json);
}
```

### After (Proposed - JSONB Binary)

```csharp
public void Insert<T>(T document) where T : class
{
    var json = DocumentSerializer.Serialize(document);
    var sql = @"
        INSERT INTO documents (id, collection_id, data)
        VALUES (@id, @collectionId, jsonb(@data))";  // Wrap with jsonb()

    _connection.Execute(sql, new {
        id = document.Id.ToByteArray(),
        collectionId = _collectionId,
        data = json  // Still serialize to JSON text, SQLite converts to JSONB
    });
}

public T FindById<T>(DocumentId id) where T : class
{
    // Option 1: Let SQLite convert back to text
    var sql = "SELECT json(data) FROM documents WHERE id = @id";
    var json = _connection.QuerySingleOrDefault<string>(sql, new { id = id.ToByteArray() });

    // Option 2: Read BLOB and convert in .NET (if needed)
    // var sql = "SELECT data FROM documents WHERE id = @id";
    // var blob = _connection.QuerySingleOrDefault<byte[]>(sql, new { id = id.ToByteArray() });
    // var json = ConvertJsonbToJson(blob);  // Would need custom parser

    return DocumentSerializer.Deserialize<T>(json);
}
```

### Query Implementation (No Changes!)

```csharp
public IEnumerable<T> Find<T>(Expression<Func<T, bool>> predicate) where T : class
{
    var whereClause = QueryTranslator.Translate(predicate);
    var sql = $@"
        SELECT json(data)
        FROM documents
        WHERE collection_id = @collectionId
          AND {whereClause}";

    var results = _connection.Query<string>(sql, new { collectionId = _collectionId });
    return results.Select(json => DocumentSerializer.Deserialize<T>(json));
}
```

---

## Version Detection

### Check SQLite Version

```sql
SELECT sqlite_version();
-- Must be >= 3.45.0 for JSONB support
```

### C# Version Check

```csharp
using Microsoft.Data.Sqlite;

public static bool SupportsJsonB(SqliteConnection connection)
{
    var versionString = connection.ExecuteScalar<string>("SELECT sqlite_version()");
    var version = Version.Parse(versionString);
    return version >= new Version(3, 45, 0);
}

// Example usage
if (!SupportsJsonB(connection))
{
    throw new NotSupportedException(
        $"SQLite version {connection.ExecuteScalar<string>("SELECT sqlite_version()")} " +
        "does not support JSONB. Version 3.45.0 or higher is required.");
}
```

---

## Performance Benchmarks (SQLite.org)

From the official documentation, JSONB provides:

### CPU Cycle Reduction

> "JSONB is both slightly smaller (by between 5% and 10% in most cases) and can be processed in less than half the number of CPU cycles compared to text JSON."

**Source**: https://sqlite.org/jsonb.html

### Why JSONB is Faster

1. **No parsing overhead**: Binary format skips text parsing
2. **Direct navigation**: Can jump to elements without scanning
3. **Type information**: Element types encoded in headers
4. **Efficient size encoding**: Payload sizes in headers avoid forward scanning

### Recommended Benchmarks

Before implementation, benchmark with representative data:

```csharp
// Benchmark 1: Insert performance
var stopwatch = Stopwatch.StartNew();
for (int i = 0; i < 10000; i++)
{
    collection.Insert(new User { Name = "John", Age = 30 });
}
stopwatch.Stop();
Console.WriteLine($"Insert 10k: {stopwatch.ElapsedMilliseconds}ms");

// Benchmark 2: Query performance
stopwatch.Restart();
var results = collection.Find(u => u.Age > 25).ToList();
stopwatch.Stop();
Console.WriteLine($"Query: {stopwatch.ElapsedMilliseconds}ms");

// Benchmark 3: Retrieval performance
stopwatch.Restart();
for (int i = 0; i < 1000; i++)
{
    collection.FindById(documentIds[i]);
}
stopwatch.Stop();
Console.WriteLine($"Retrieve 1k: {stopwatch.ElapsedMilliseconds}ms");
```

---

## Migration Strategy

### Step 1: Create Migration Tool

```csharp
public class JsonbMigrator
{
    public void MigrateDatabase(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        // Check version
        if (!SupportsJsonB(connection))
            throw new NotSupportedException("SQLite 3.45.0+ required");

        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Add new BLOB column
            connection.Execute("ALTER TABLE documents ADD COLUMN data_new BLOB");

            // 2. Convert all rows
            connection.Execute(@"
                UPDATE documents
                SET data_new = jsonb(data)");

            // 3. Drop old column and rename new one
            connection.Execute("ALTER TABLE documents DROP COLUMN data");
            connection.Execute("ALTER TABLE documents RENAME COLUMN data_new TO data");

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

### Step 2: Validate Migration

```csharp
public bool ValidateMigration(SqliteConnection connection)
{
    // Check that data column is BLOB type
    var sql = "SELECT typeof(data) FROM documents LIMIT 1";
    var type = connection.QuerySingleOrDefault<string>(sql);

    if (type != "blob")
    {
        Console.WriteLine($"Error: data column is '{type}', expected 'blob'");
        return false;
    }

    // Verify all documents can be queried
    var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM documents");
    var validCount = connection.ExecuteScalar<int>(
        "SELECT COUNT(*) FROM documents WHERE json_type(data) IS NOT NULL");

    if (count != validCount)
    {
        Console.WriteLine($"Error: {count - validCount} documents have invalid JSONB");
        return false;
    }

    return true;
}
```

---

## Important Notes

### JSONB is Internal Format

From SQLite documentation:

> "JSONB is not intended as an external format to be used by applications. JSONB is designed for internal use by SQLite only."

**Implication**: Always access JSONB through SQLite's JSON functions. Don't try to parse JSONB blobs directly in .NET.

### Not Compatible with PostgreSQL

> "The 'JSONB' name is inspired by PostgreSQL, however the on-disk format for SQLite's JSONB is not the same as PostgreSQL. The two formats have the same name, but wildly different internal representations and are not in any way binary compatible."

**Implication**: Don't try to use tools that work with PostgreSQL JSONB.

### Reading JSONB Data

To read JSONB data as text:

```sql
-- Wrong: Reading JSONB directly returns binary blob
SELECT data FROM documents WHERE id = ?;

-- Right: Convert to text first
SELECT json(data) FROM documents WHERE id = ?;
```

### Debugging JSONB Data

For debugging, always convert to text:

```sql
-- View JSONB as formatted JSON
SELECT json_pretty(data) FROM documents;

-- Check JSONB validity
SELECT json_valid(data) FROM documents;

-- Get JSONB structure
SELECT json_tree(data) FROM documents;
```

---

## References

### Official SQLite Documentation
- **JSONB Format**: https://sqlite.org/jsonb.html
- **JSON Functions**: https://sqlite.org/json1.html
- **JSONB Specification**: https://github.com/sqlite/sqlite/blob/master/doc/jsonb.md
- **Version History**: https://sqlite.org/changes.html
- **Release Notes 3.45.0**: https://sqlite.org/releaselog/3_45_0.html

### Articles and Analysis
- **Fedora Magazine**: [JSON and JSONB support in SQLite](https://fedoramagazine.org/json-and-jsonb-support-in-sqlite-3-45-0/)
- **CCL Solutions**: [SQLite's New Binary JSON Format](https://www.cclsolutionsgroup.com/post/sqlites-new-binary-json-format)
- **DevClass**: [SQLite's new support for binary JSON](https://devclass.com/2024/01/16/sqlites-new-support-for-binary-json-is-similar-but-different-from-a-postgresql-feature/)
- **Beekeeper Studio**: [How To Store And Query JSON in SQLite Using A BLOB Column](https://www.beekeeperstudio.io/blog/sqlite-json-with-blob)

### Discussion Forums
- **SQLite Forum**: [JSONB has landed](https://sqlite.org/forum/forumpost/fa6f64e3dc1a5d97)
- **Hacker News**: Search "SQLite JSONB" for community discussions

### Microsoft.Data.Sqlite
- **NuGet Package**: https://www.nuget.org/packages/Microsoft.Data.Sqlite
- **Documentation**: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
- **Supported SQLite Versions**: Check package dependencies for bundled SQLite version

---

## Quick Reference Card

```sql
-- Convert text to JSONB
jsonb('{"key":"value"}')

-- Convert JSONB to text
json(jsonb_data)

-- Query (works with both)
json_extract(data, '$.key')

-- Check type
typeof(data)  -- 'blob' or 'text'

-- Validate
json_valid(data)

-- Pretty print
json_pretty(data)

-- Create JSONB object
jsonb_object('name', 'John', 'age', 30)

-- Create JSONB array
jsonb_array(1, 2, 3)

-- Modify JSONB
jsonb_set(data, '$.key', 'value')
jsonb_insert(data, '$.key', 'value')
jsonb_replace(data, '$.key', 'value')
jsonb_remove(data, '$.key')
```

---

**End of Reference Document**
