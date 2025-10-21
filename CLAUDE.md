# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Codezerg.DocumentStore is a document-oriented data layer for SQLite that provides NoSQL-style document storage with LINQ query support. It targets .NET Standard 2.0 for broad compatibility.

## Build and Development Commands

### Building
```bash
# Build entire solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Create NuGet package
dotnet pack -c Release
```

### Running Samples
```bash
# Run the main sample application
dotnet run --project samples/SampleApp/SampleApp.csproj

# Test System.Data.SQLite provider compatibility
dotnet run --project samples/SystemDataSQLiteTest/SystemDataSQLiteTest.csproj
```

### Testing
```bash
# Run tests (note: test project was recently removed)
dotnet test
```

## Architecture

### Core Design Patterns

**Connection-per-Operation Pattern**: The library creates and disposes a new database connection for each operation. This provides thread-safety and proper resource cleanup, but means in-memory databases (`:memory:`) are NOT supported.

**Two-Tier Dependency Injection**:
1. `AddSqliteDatabase()` - Registers `ISqliteConnectionProvider` with connection settings
2. `AddDocumentDatabase()` - Registers `IDocumentDatabase` with document-specific options

### Key Components

**SqliteConnectionProvider** (`ISqliteConnectionProvider`)
- Creates database connections using ADO.NET provider factories
- Supports both `Microsoft.Data.Sqlite` (default) and `System.Data.SQLite` providers
- Applies SQLite pragmas (journal_mode, page_size, synchronous) to each connection
- Connection string and provider name configured via `SqliteDatabaseOptions`

**SqliteDocumentDatabase** (`IDocumentDatabase`)
- Main database interface for managing collections
- Initializes 4-table schema: `collections`, `documents`, `indexes`, `indexed_values`
- Supports JSONB (binary JSON) storage for 20-76% faster operations
- Optional collection caching via `CachedDocumentCollection` decorator
- Does NOT implement `IDisposable` (no persistent connections to dispose)

**SqliteDocumentCollection** (`IDocumentCollection<T>`)
- CRUD operations on document collections
- Query execution with LINQ translation
- Index management (regular and unique indexes)
- Bulk operations (InsertMany, UpdateMany, DeleteMany)

**QueryTranslator**
- Converts LINQ `Expression<Func<T, bool>>` to SQLite WHERE clauses
- Translates property access to `json_extract(data, '$.propertyPath')`
- Converts PascalCase to camelCase for JSON paths
- Supports operators: ==, !=, >, >=, <, <=, &&, ||
- Supports methods: Contains, StartsWith, EndsWith
- Generates parameterized queries to prevent SQL injection

**DocumentId**
- 12-byte unique identifier (4-byte timestamp + 5-byte random + 3-byte counter)
- Provides temporal ordering without database round-trips
- Compatible with MongoDB ObjectId format
- Internally uses `ObjectId` helper class

### Database Schema

```sql
-- Collections table
collections (id, name, created_at)

-- Documents table (data column is BLOB for JSONB, TEXT for legacy)
documents (id, collection_id, document_id, data, created_at, updated_at, version)

-- Indexes table
indexes (id, collection_id, name, fields, unique_index, sparse)

-- Indexed values table for custom indexes
indexed_values (document_id, index_id, field_path, value_text, value_number, value_boolean, value_type)
```

### Configuration Options

**SqliteDatabaseOptions** (connection-level):
- `ProviderName`: ADO.NET provider ("Microsoft.Data.Sqlite" or "System.Data.SQLite")
- `ConnectionString`: SQLite connection string
- `JournalMode`: WAL (default), DELETE, TRUNCATE, etc.
- `PageSize`: 4096 (default), must be power of 2 between 512-65536
- `Synchronous`: NORMAL (default), FULL, OFF

**DocumentDatabaseOptions** (database-level):
- `UseJsonB`: Enable JSONB binary storage (default: true)
- `CachedCollectionPredicates`: Functions to determine which collections to cache

### Collection Caching

The `CachedDocumentCollection<T>` decorator wraps `SqliteDocumentCollection<T>` to provide in-memory caching:
- Configured via `CacheCollection(name)` or `CacheCollections(predicate)` in options builder
- Loads entire collection into memory on first access
- Automatically syncs cache on insert/update/delete operations
- Best for small, frequently-accessed collections

## Important Coding Patterns

### Provider Setup

When instantiating `SqliteDocumentDatabase`, you must provide both connection provider and database options:

```csharp
var connectionOptions = Options.Create(new SqliteDatabaseOptions
{
    ProviderName = "Microsoft.Data.Sqlite",
    ConnectionString = "Data Source=myapp.db",
    JournalMode = "WAL"
});

var connectionProvider = new SqliteConnectionProvider(connectionOptions);

var databaseOptions = Options.Create(new DocumentDatabaseOptions
{
    UseJsonB = true
});

var db = new SqliteDocumentDatabase(connectionProvider, databaseOptions);
```

### Dependency Injection Setup

```csharp
services.AddSqliteDatabase(options => options
    .UseConnectionString("Data Source=myapp.db")
    .UseProvider("Microsoft.Data.Sqlite")
    .UseJournalMode("WAL"));

services.AddDocumentDatabase(options => options
    .UseJsonB(true)
    .CacheCollection("users")
    .CacheCollection("products"));
```

### Query Translation Examples

LINQ expressions are translated to SQLite JSON operations:
- `u => u.Name == "Alice"` → `json_extract(data, '$.name') = @p0`
- `u => u.Age > 30` → `json_extract(data, '$.age') > @p0`
- `u => u.Address.City == "Seattle"` → `json_extract(data, '$.address.city') = @p0`
- `u => u.Email.Contains("@gmail")` → `json_extract(data, '$.email') LIKE @p0` (with `%@gmail%`)

### JSONB vs JSON Text Storage

JSONB (default) stores documents as binary blobs, providing significant performance improvements. The `documents.data` column is created as:
- `BLOB` when `UseJsonB = true` (default)
- `TEXT` when `UseJsonB = false` (legacy)

This choice must be made at database initialization and cannot be changed later without migration.

## Common Gotchas

1. **In-memory databases not supported**: The connection-per-operation pattern means each `:memory:` connection creates a separate database. Always use file-based databases.

2. **Provider registration**: When using DI, `AddSqliteDatabase()` must be called before `AddDocumentDatabase()` since the database depends on the connection provider.

3. **No IDisposable**: `SqliteDocumentDatabase` does NOT implement `IDisposable` because it maintains no persistent connections. Each operation creates and disposes its own connection automatically. Don't use `using` statements.

4. **PascalCase to camelCase**: Property names are automatically converted to camelCase in JSON storage (e.g., `Name` → `name`). The QueryTranslator handles this automatically.

5. **Index performance**: Creating indexes on frequently queried fields significantly improves performance. Unique indexes also enforce data integrity.

6. **Bulk operations**: Use `InsertManyAsync` instead of multiple `InsertOneAsync` calls for better performance (optimized with transactions).

## Recent Changes

Based on recent commits:
- Added support for System.Data.SQLite provider (in addition to Microsoft.Data.Sqlite)
- Replaced custom exceptions with standard .NET exceptions
- Refactored DocumentId to use ObjectId helper internally
- Optimized bulk insert operations
- Tests were removed from the repository
