# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Codezerg.DocumentStore is a document-oriented data layer for SQLite that provides flexible, schema-less storage with full embedding. It uses SQLite's JSON capabilities to store and query documents similar to MongoDB but with the simplicity and portability of SQLite.

**Target Framework**: .NET Standard 2.0 (main library), .NET 9.0 (tests and samples)

**Key Dependencies**: Microsoft.Data.Sqlite, Dapper, System.Text.Json, Microsoft.Extensions.Logging.Abstractions, Polly

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

The library follows an interface-based design pattern:

- **IDocumentDatabase**: Main entry point for database operations (create/drop collections, transactions)
- **IDocumentCollection&lt;T&gt;**: Collection interface providing CRUD operations, queries, and indexing
- **IDocumentTransaction**: Transaction support for atomic operations across collections

### Constructors

SqliteDocumentDatabase provides two constructors:

1. **String constructor**: `SqliteDocumentDatabase(string connectionString, ILogger<SqliteDocumentDatabase>? logger = null)`
   - Direct instantiation with a connection string
   - Used for simple scenarios and testing

2. **Options constructor**: `SqliteDocumentDatabase(IOptions<DocumentDatabaseOptions> options, ILogger<SqliteDocumentDatabase>? logger = null)`
   - Supports Microsoft.Extensions.Options pattern
   - Used for dependency injection scenarios
   - Internally delegates to the string constructor after extracting and validating the connection string

### Database Schema

SQLite uses a **centralized schema** with four main tables:

1. **collections**: Stores collection metadata (id, name, created_at)
2. **documents**: Stores document JSON data with foreign key to collections
3. **indexes**: Stores index definitions per collection
4. **indexed_values**: Stores extracted index values for fast lookups

All tables use cascading deletes to maintain referential integrity.

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

### Transaction Support

Transactions wrap SQLite transactions and can be used across multiple collection operations. Transactions must be explicitly committed or they are rolled back on dispose.

### Serialization

Documents are serialized to JSON using System.Text.Json with:
- CamelCase property naming convention
- DocumentId custom converter (DocumentIdJsonConverter)
- Automatic timestamps (CreatedAt, UpdatedAt) when documents have these properties

## Key Implementation Files

- **SqliteDocumentDatabase.cs**: Main database implementation, manages connections and schema
- **SqliteDocumentCollection.cs**: Collection operations, query execution, index management
- **QueryTranslator.cs**: LINQ to SQL translation engine
- **DocumentId.cs**: Unique identifier implementation
- **DocumentSerializer.cs**: JSON serialization configuration
- **Compat.cs**: .NET Standard 2.0 compatibility helpers

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
// Register with connection string for file-based database
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=myapp.db"));

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
}
```

The database is registered as a singleton and uses the `IOptions<DocumentDatabaseOptions>` constructor overload internally.

## Testing Guidelines

Tests use xUnit and follow the pattern:
- In-memory databases for test isolation (`new SqliteDocumentDatabase("Data Source=:memory:")`)
- Each test creates its own database instance
- Tests cover CRUD operations, queries, transactions, and edge cases

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

### Connection Management

- SqliteDocumentDatabase maintains a single persistent connection
- The connection is opened in the constructor and must be disposed properly
- Collections share the same connection instance
- Transactions use the same connection to ensure ACID properties

## Additional Resources

### SQLite JSONB Binary Format

For information about SQLite's binary JSON format (JSONB) and potential future optimization strategies, see:

**[docs/SQLITE_JSONB_REFERENCE.md](docs/SQLITE_JSONB_REFERENCE.md)** - Comprehensive reference covering:
- JSONB binary encoding format and performance benefits
- SQL function reference for JSONB operations
- Migration strategies from JSON text to JSONB
- Version detection and compatibility requirements (SQLite 3.45.0+)
- Implementation examples for Codezerg.DocumentStore

This document provides detailed guidance for potentially migrating from JSON text storage to JSONB binary format for improved performance (approximately 50% CPU reduction according to SQLite benchmarks).
