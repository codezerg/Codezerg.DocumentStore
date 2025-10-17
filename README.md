# Codezerg.DocumentStore

Document-oriented data layer for SQLite. Store and query JSON documents with LINQ support.

## Features

- **Document storage**: Store POCOs as JSON documents without predefined schemas
- **LINQ queries**: Type-safe queries translated to SQLite JSON operations
- **Indexing**: Regular and unique indexes for fast lookups
- **JSONB storage**: Binary JSON format for 20-76% faster operations (enabled by default)
- **Collection caching**: Optional in-memory caching for improved read performance
- **DocumentId**: 12-byte unique identifier with timestamp ordering
- **.NET Standard 2.0**: Broad compatibility across .NET platforms

## Installation

```bash
dotnet add package Codezerg.DocumentStore
```

## Quick Start

### 1. Define your document model

```csharp
public class User
{
    public DocumentId Id { get; set; }  // Auto-assigned on insert
    public string Name { get; set; }
    public string Email { get; set; }
    public int Age { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string State { get; set; }
}
```

### 2. Create a database and collection

```csharp
using Codezerg.DocumentStore;

using var db = new SqliteDocumentDatabase("Data Source=myapp.db");
var users = await db.GetCollectionAsync<User>("users");
```

### 3. Insert documents

```csharp
var user = new User
{
    Name = "Alice Smith",
    Email = "alice@example.com",
    Age = 30,
    Address = new Address
    {
        City = "Seattle",
        State = "WA"
    }
};

await users.InsertOneAsync(user);  // Id is auto-assigned
```

### 4. Query documents

```csharp
var user = await users.FindByIdAsync(userId);
var adults = await users.FindAsync(u => u.Age >= 18);
var seattleUsers = await users.FindAsync(u => u.Address.City == "Seattle");
var gmailUsers = await users.FindAsync(u => u.Email.Contains("@gmail.com"));
var results = await users.FindAsync(u => u.Age > 25 && u.Address.State == "WA");
var page = await users.FindAsync(u => u.Age > 0, skip: 10, limit: 20);
var allUsers = await users.FindAllAsync();
```

### 5. Update and delete

```csharp
user.Age = 31;
await users.UpdateByIdAsync(user.Id, user);

await users.DeleteByIdAsync(user.Id);
```

### 6. Create indexes

```csharp
await users.CreateIndexAsync(u => u.Email);
await users.CreateIndexAsync(u => u.Email, unique: true);
```

## Advanced Usage

### Connection Management

The library uses a **connection-per-operation pattern** where each database operation creates and disposes its own connection. This provides:
- Thread-safe operations without shared connection state
- Proper resource cleanup after each operation
- Better isolation between operations

**Important**: In-memory databases (`:memory:`) are not supported because each new connection creates a separate in-memory database. Always use file-based databases:

```csharp
// Recommended: File-based database
using var db = new SqliteDocumentDatabase("Data Source=myapp.db");

// NOT supported with connection-per-operation
// using var db = new SqliteDocumentDatabase("Data Source=:memory:");
```

### Dependency Injection

```csharp
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=myapp.db")
           .UseJsonB(true)
           .CacheCollection("users"));

public class MyService
{
    private readonly IDocumentDatabase _database;

    public MyService(IDocumentDatabase database)
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

### Working with DocumentId

```csharp
var id = DocumentId.Parse("507f1f77bcf86cd799439011");
DateTime timestamp = id.Timestamp;
var newId = DocumentId.NewId();
string idString = id.ToString();
```

### Collection Management

```csharp
var collections = await db.ListCollectionNamesAsync();
await db.DropCollectionAsync("users");
var allCollections = await db.ListCollectionNamesAsync();
bool exists = allCollections.Contains("users");
```

### JSONB Performance

JSONB binary storage is enabled by default and provides significant performance benefits:

```csharp
// JSONB enabled (default) - 20-76% faster
var db = new SqliteDocumentDatabase("Data Source=app.db");

// Disable JSONB if needed (uses JSON text storage)
var db = new SqliteDocumentDatabase("Data Source=app.db", useJsonB: false);
```

### Collection Caching

Enable in-memory caching for frequently accessed collections:

```csharp
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=app.db")
           .CacheCollection("users")
           .CacheCollection("products")
           .CacheCollections(name => name.StartsWith("hot_")));
```

## Query Translation

| C# Expression | SQLite Translation |
|--------------|-------------------|
| `u => u.Name == "Alice"` | `json_extract(data, '$.name') = 'Alice'` |
| `u => u.Age > 30` | `json_extract(data, '$.age') > 30` |
| `u => u.Address.City == "Seattle"` | `json_extract(data, '$.address.city') = 'Seattle'` |
| `u => u.Email.Contains("@gmail")` | `json_extract(data, '$.email') LIKE '%@gmail%'` |
| `u => u.Name.StartsWith("A")` | `json_extract(data, '$.name') LIKE 'A%'` |

## Development

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run sample application
dotnet run --project samples/SampleApp/SampleApp.csproj

# Run benchmarks
dotnet run --project benchmarks/Codezerg.DocumentStore.Benchmarks/Codezerg.DocumentStore.Benchmarks.csproj -c Release

# Create NuGet package
dotnet pack -c Release
```

## Architecture

### DocumentId Format

12-byte unique identifier (4-byte timestamp + 5-byte random + 3-byte counter) providing temporal ordering and uniqueness without database round-trips.

### Database Schema

Four tables: `collections`, `documents`, `indexes`, `indexed_values` with cascading deletes.

The `documents` table stores data as:
- **BLOB** when JSONB is enabled (default) for binary JSON storage
- **TEXT** when JSONB is disabled for traditional JSON text storage

### Performance

JSONB binary storage provides:
- **20-76% faster** operations (inserts, queries, updates)
- **5-10% smaller** storage size
- Seamless integration with all json_extract() queries

See `benchmarks/` directory for detailed performance comparisons.

## License

MIT
