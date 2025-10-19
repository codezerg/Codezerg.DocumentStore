# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Codezerg.DocumentStore is a document-oriented data layer for SQLite that provides flexible, schema-less storage with full embedding. It uses SQLite's JSON capabilities to store and query documents similar to MongoDB but with the simplicity and portability of SQLite.

**Target Framework**: .NET Standard 2.0 (main library), .NET 9.0 (tests and samples)

**Package Version**: 1.0.0

**Key Dependencies**:
- Microsoft.Data.Sqlite 9.0.10
- Dapper 2.1.66
- System.Text.Json 9.0.10
- Microsoft.Extensions.Logging.Abstractions 9.0.10
- Microsoft.Extensions.DependencyInjection.Abstractions 9.0.10
- Microsoft.Extensions.Options 9.0.10

## Recent Changes

The following significant changes have been made to the codebase:

- **Transaction support removed**: All transaction-related parameters and `IDocumentTransaction` interface have been removed for API simplification
- **Async collection access**: `GetCollection<T>()` changed to `GetCollectionAsync<T>()` to support async initialization
- **JSONB storage support**: Binary JSON storage (JSONB) is now supported and enabled by default for 20-76% performance improvements
- **Collection caching**: In-memory caching support added via `CachedDocumentCollection<T>` for improved read performance
- **Advanced configuration**: New SQLite pragma options (JournalMode, PageSize, Synchronous) for performance tuning
- **Benchmarking infrastructure**: Comprehensive benchmarks added to measure performance with/without JSONB and caching

## Build and Test Commands

### Build the solution
```bash
dotnet build
```

### Build in Release mode
```bash
dotnet build -c Release
```

### Run tests
```bash
dotnet test
```

### Run tests with detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run a specific test
```bash
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Run the sample application
```bash
dotnet run --project samples/SampleApp/SampleApp.csproj
```

### Create NuGet package
```bash
dotnet pack -c Release
```

## Architecture

### Core Interfaces

The library follows an interface-based design pattern with fully async APIs:

#### IDocumentDatabase
Main entry point for database operations. Key methods:
- `Task<IDocumentCollection<T>> GetCollectionAsync<T>(string name)` - Get or create a collection (async)
- `Task CreateCollectionAsync<T>(string name)` - Explicitly create a collection
- `Task DropCollectionAsync(string name)` - Remove a collection and all its documents
- `Task<List<string>> ListCollectionNamesAsync()` - List all collections
- `string DatabaseName { get; }` - Get the database name
- `string ConnectionString { get; }` - Get the connection string

#### IDocumentCollection&lt;T&gt;
Type-safe collection operations:

**Insert Operations:**
- `Task InsertOneAsync(T document)` - Insert a single document
- `Task InsertManyAsync(IEnumerable<T> documents)` - Insert multiple documents

**Query Operations:**
- `Task<T?> FindByIdAsync(DocumentId id)` - Find by ID
- `Task<T?> FindOneAsync(Expression<Func<T, bool>> filter)` - Find first matching document
- `Task<List<T>> FindAsync(Expression<Func<T, bool>> filter)` - Find all matching documents
- `Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, int skip, int limit)` - Find with pagination
- `Task<List<T>> FindAllAsync()` - Find all documents
- `Task<long> CountAsync(Expression<Func<T, bool>> filter)` - Count matching documents
- `Task<long> CountAllAsync()` - Count all documents
- `Task<bool> AnyAsync(Expression<Func<T, bool>> filter)` - Check if any match

**Update Operations:**
- `Task<bool> UpdateByIdAsync(DocumentId id, T document)` - Update by ID
- `Task<bool> UpdateOneAsync(Expression<Func<T, bool>> filter, T document)` - Update first match
- `Task<long> UpdateManyAsync(Expression<Func<T, bool>> filter, Action<T> updateAction)` - Update multiple documents

**Delete Operations:**
- `Task<bool> DeleteByIdAsync(DocumentId id)` - Delete by ID
- `Task<bool> DeleteOneAsync(Expression<Func<T, bool>> filter)` - Delete first match
- `Task<long> DeleteManyAsync(Expression<Func<T, bool>> filter)` - Delete all matching

**Index Operations:**
- `Task CreateIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false)` - Create an index
- `Task DropIndexAsync<TField>(Expression<Func<T, TField>> fieldExpression)` - Drop an index

**Properties:**
- `string CollectionName { get; }` - Get the collection name

### Constructors

SqliteDocumentDatabase provides two constructors:

1. **String constructor**: `SqliteDocumentDatabase(string connectionString, ILogger<SqliteDocumentDatabase>? logger = null, bool useJsonB = true)`
   - Direct instantiation with a connection string
   - Used for simple scenarios and testing
   - `useJsonB` parameter enables JSONB binary storage (default: true)

2. **Options constructor**: `SqliteDocumentDatabase(IOptions<DocumentDatabaseOptions> options, ILogger<SqliteDocumentDatabase>? logger = null)`
   - Supports Microsoft.Extensions.Options pattern
   - Used for dependency injection scenarios
   - Internally delegates to the private constructor after extracting and validating options

### Database Schema

SQLite uses a **centralized schema** with four main tables:

1. **collections**: Stores collection metadata (id, name, created_at)
2. **documents**: Stores document data (BLOB for JSONB or TEXT for JSON) with foreign key to collections
3. **indexes**: Stores index definitions per collection
4. **indexed_values**: Stores extracted index values for fast lookups

All tables use cascading deletes to maintain referential integrity.

**Note**: The `data` column in the `documents` table is BLOB when JSONB is enabled (default), or TEXT when JSONB is disabled.

### DocumentId System

DocumentId is a 12-byte unique identifier inspired by MongoDB ObjectId:
- **4 bytes**: Unix timestamp (allows chronological sorting)
- **5 bytes**: Random value
- **3 bytes**: Incrementing counter

This provides temporal ordering and uniqueness guarantees without requiring database round-trips.

### Query Translation

The **QueryTranslator** class (src/Codezerg.DocumentStore/QueryTranslator.cs) converts LINQ expressions to SQLite WHERE clauses:

- Member access (e.g., `u => u.Name`) translates to `json_extract(data, '$.name')`
- Property names are converted to camelCase for JSON storage
- Supports: equality, comparison operators, logical operators (AND/OR), NOT
- String methods: Contains, StartsWith, EndsWith (translated to LIKE)
- Nested property access: `u => u.Address.City` becomes `json_extract(data, '$.address.city')`

### Serialization

Documents are serialized to JSON using System.Text.Json with:
- CamelCase property naming convention
- DocumentId custom converter (DocumentIdJsonConverter)
- Automatic timestamps (CreatedAt, UpdatedAt) when documents have these properties

### JSONB Storage

JSONB (binary JSON) storage is enabled by default and provides significant performance improvements:
- **20-76% faster** operations (inserts, queries, updates)
- **5-10% smaller** storage size
- SQLite automatically converts JSON text to/from JSONB binary format
- All json_extract() queries work seamlessly with JSONB data

To disable JSONB and use text storage:
```csharp
var db = new SqliteDocumentDatabase("Data Source=app.db", useJsonB: false);
```

### Collection Caching

In-memory caching can be enabled per collection for improved read performance:
- Caches documents in memory after first load
- Transparent cache invalidation on writes
- Configurable via `CacheCollection()` or `CacheCollections()` in options builder

Example:
```csharp
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=app.db")
           .CacheCollection("users")
           .CacheCollections(name => name.StartsWith("cache_")));
```

### Error Handling

The library uses standard .NET exceptions for error conditions:
- **InvalidOperationException**: Thrown when a unique index constraint is violated (duplicate key errors)
- **NotSupportedException**: Thrown when a LINQ query expression cannot be translated to SQL
- **ArgumentNullException**: Thrown when null arguments are passed to methods that require non-null values

## Key Implementation Files

All source files are located in `src/Codezerg.DocumentStore/`:

### Core Implementation
- **SqliteDocumentDatabase.cs**: Main database implementation, manages connections and schema
- **SqliteDocumentCollection.cs**: Collection operations, query execution, index management
- **QueryTranslator.cs**: LINQ to SQL translation engine
- **DocumentId.cs**: Unique identifier implementation (12-byte ObjectId-inspired)
- **Compat.cs**: .NET Standard 2.0 compatibility helpers

### Interfaces
- **IDocumentDatabase.cs**: Database interface defining collection management
- **IDocumentCollection.cs**: Collection interface defining CRUD, query, and index operations

### Dependency Injection
- **ServiceCollectionExtensions.cs**: Extension methods for registering database with DI container (`AddDocumentDatabase`)

### Configuration (`Configuration/` subdirectory)
- **DocumentDatabaseOptions.cs**: Configuration options for database setup (ConnectionString, UseJsonB, JournalMode, PageSize, Synchronous, CachedCollectionPredicates)
- **DocumentDatabaseOptionsBuilder.cs**: Fluent builder for configuration options with methods like UseJsonB(), UseJournalMode(), UsePageSize(), UseSynchronous(), CacheCollection(), CacheCollections()

### Caching (`Caching/` subdirectory)
- **CachedDocumentCollection.cs**: In-memory caching wrapper for IDocumentCollection<T> that caches documents by ID

### Serialization (`Serialization/` subdirectory)
- **DocumentSerializer.cs**: JSON serialization configuration with camelCase naming
- **DocumentIdJsonConverter.cs**: Custom JSON converter for DocumentId type


## Common Development Patterns

### Creating Documents

Documents are POCOs with a `DocumentId Id` property:

```csharp
public class User
{
    public DocumentId Id { get; set; }  // Auto-assigned on insert
    public string Name { get; set; }
    public int Age { get; set; }
}
```

### Query Patterns

The library supports LINQ-based queries that are translated to SQLite JSON operations:

```csharp
// Simple filter
users.Find(u => u.Age > 30)

// Complex filter with AND/OR
users.Find(u => u.Age > 30 && u.City == "Seattle")

// String operations
users.Find(u => u.Email.Contains("@example.com"))

// Pagination
users.Find(u => u.Age > 0, skip: 10, limit: 20)
```

### Index Creation

Indexes improve query performance on frequently accessed fields:

```csharp
// Create regular index
users.CreateIndex(u => u.City)

// Create unique index
users.CreateIndex(u => u.Email, unique: true)
```

### Dependency Injection

The library supports Microsoft.Extensions.DependencyInjection via the `AddDocumentDatabase` extension method:

```csharp
// Basic registration with connection string
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=myapp.db"));

// Advanced configuration with JSONB, caching, and pragma settings
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=myapp.db")
           .UseJsonB(true)
           .UseJournalMode("WAL")
           .UsePageSize(4096)
           .UseSynchronous("NORMAL")
           .CacheCollection("users")
           .CacheCollections(name => name.StartsWith("hot_")));

// For in-memory databases
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=:memory:"));

// Inject IDocumentDatabase into services
public class UserService
{
    private readonly IDocumentDatabase _database;

    public UserService(IDocumentDatabase database)
    {
        _database = database;
    }

    public async Task<User?> GetUserAsync(DocumentId id)
    {
        var users = await _database.GetCollectionAsync<User>("users");
        return await users.FindByIdAsync(id);
    }
}
```

The database is registered as a singleton and uses the `IOptions<DocumentDatabaseOptions>` constructor overload internally.

## Testing Guidelines

Tests use xUnit and follow the pattern:
- **File-based databases** for test isolation (in-memory databases don't work with connection-per-operation)
- Each test class creates a temporary database file in the system temp directory
- `IAsyncLifetime` is used for async initialization of collections via `GetCollectionAsync`
- Cleanup happens in `DisposeAsync()` which deletes the temporary database file
- Tests cover CRUD operations, queries, indexes, and edge cases

Example test class structure:
```csharp
public class MyTests : IAsyncLifetime
{
    private readonly string _dbFile;
    private readonly SqliteDocumentDatabase _database;
    private IDocumentCollection<MyDocument> _collection = null!;

    public MyTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _database = new SqliteDocumentDatabase($"Data Source={_dbFile}");
    }

    public async Task InitializeAsync()
    {
        _collection = await _database.GetCollectionAsync<MyDocument>("myCollection");
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
}
```

## Important Considerations

### .NET Standard 2.0 Compatibility

The main library targets .NET Standard 2.0 for broad compatibility. Use the **Compat.cs** helper class for features not available in .NET Standard 2.0:
- `Compat.GetRandomInt32()` instead of `Random.Shared.Next()`
- `Compat.FillRandom()` instead of `RandomNumberGenerator.Fill()`
- `Compat.FromHexString()` / `Compat.ToHexString()` instead of `Convert` methods

### Query Limitations

The QueryTranslator has specific limitations:
- Only supports basic LINQ expressions (member access, binary operators, method calls)
- Array/collection queries (except Contains) are not yet supported
- Complex projections and joins are not supported
- Custom methods cannot be used in expressions

### Transactions

**Note**: Transaction support has been removed from the current API for simplicity. All operations execute immediately and are not part of an explicit transaction context. If you need transactional behavior, consider:
- Using SQLite's implicit transaction handling for single operations
- Implementing application-level compensation logic for multi-step operations
- Future versions may reintroduce transaction support based on user feedback

### Connection Management

- **Connection-per-operation pattern**: Each database operation creates and disposes its own connection
- No persistent connections are maintained - connections are created on-demand via `CreateConnection()`
- SQLite pragmas (JournalMode, PageSize, Synchronous) are applied to each new connection
- Thread-safe schema initialization ensures database tables are created only once
- **Important**: In-memory databases (`:memory:`) are not compatible with this pattern - always use file-based databases

## Project Structure

```
Codezerg.DocumentStore/
├── src/
│   ├── Codezerg.DocumentStore/              (Main Library - .NET Standard 2.0)
│   │   ├── SqliteDocumentDatabase.cs
│   │   ├── SqliteDocumentCollection.cs
│   │   ├── QueryTranslator.cs
│   │   ├── DocumentId.cs
│   │   ├── IDocumentDatabase.cs
│   │   ├── IDocumentCollection.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── Compat.cs
│   │   ├── Caching/
│   │   │   └── CachedDocumentCollection.cs
│   │   ├── Configuration/
│   │   │   ├── DocumentDatabaseOptions.cs
│   │   │   └── DocumentDatabaseOptionsBuilder.cs
│   │   └── Serialization/
│   │       ├── DocumentSerializer.cs
│   │       └── DocumentIdJsonConverter.cs
│   │
│   └── Codezerg.DocumentStore.Tests/        (Test Suite - .NET 9.0)
│       ├── SqliteDocumentDatabaseTests.cs
│       ├── SqliteDocumentCollectionTests.cs
│       ├── DocumentIdTests.cs
│       ├── IndexTests.cs
│       └── JsonBTests.cs
│
├── benchmarks/
│   └── Codezerg.DocumentStore.Benchmarks/   (Performance Benchmarks - .NET 9.0)
│       └── Various benchmark classes
│
├── docs/
│   └── SQLITE_JSONB_REFERENCE.md            (JSONB reference documentation)
│
└── samples/
    └── SampleApp/                           (.NET 9.0)
        └── Program.cs                       (Comprehensive examples)
```

## Test Coverage

The test suite includes comprehensive tests across multiple test files:

- **SqliteDocumentDatabaseTests.cs**: Database creation, collection management
- **SqliteDocumentCollectionTests.cs**: CRUD operations, queries, indexes, pagination, complex filters
- **DocumentIdTests.cs**: ID generation, parsing, comparison, equality, timestamp validation
- **IndexTests.cs**: Index creation, unique constraints, performance testing
- **JsonBTests.cs**: JSONB storage format validation and testing

## Benchmarks

The benchmarks project (benchmarks/Codezerg.DocumentStore.Benchmarks) provides performance measurements for:
- Insert operations (with/without JSONB)
- Query operations (FindById, Find with filters)
- Update operations (UpdateById, UpdateMany)
- Mixed workloads (realistic usage patterns)
- Caching performance impact

Run benchmarks with:
```bash
dotnet run --project benchmarks/Codezerg.DocumentStore.Benchmarks/Codezerg.DocumentStore.Benchmarks.csproj -c Release
```
