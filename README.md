# Codezerg.DocumentStore

A document-oriented data layer for SQLite that provides flexible, schema-less storage with full .NET embedding. Store and query JSON documents with the simplicity and portability of SQLite.

## Features

- **Document-oriented storage**: Store POCOs as JSON documents without predefined schemas
- **LINQ query support**: Write type-safe queries that translate to SQLite JSON operations
- **Indexing**: Create regular and unique indexes on document properties for fast lookups
- **Transactions**: Full ACID transaction support across multiple collections
- **Unique document IDs**: Built-in DocumentId type with timestamp ordering and uniqueness
- **Automatic timestamps**: Optional CreatedAt/UpdatedAt tracking
- **SQLite powered**: Leverages SQLite's JSON capabilities for reliability and portability
- **.NET Standard 2.0**: Compatible with .NET Framework, .NET Core, and .NET 5+

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

// Create or open a database
using var db = new SqliteDocumentDatabase("myapp.db");

// Get a collection (created automatically if it doesn't exist)
var users = db.GetCollection<User>("users");
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

users.Insert(user);
// user.Id is now automatically assigned
```

### 4. Query documents

```csharp
// Find by ID
var user = users.FindById(userId);

// Find with LINQ expressions
var adults = users.Find(u => u.Age >= 18);
var seattleUsers = users.Find(u => u.Address.City == "Seattle");
var gmailUsers = users.Find(u => u.Email.Contains("@gmail.com"));

// Complex queries
var results = users.Find(u => u.Age > 25 && u.Address.State == "WA");

// Pagination
var page = users.Find(u => u.Age > 0, skip: 10, limit: 20);

// Get all documents
var allUsers = users.FindAll();
```

### 5. Update and delete

```csharp
// Update a document
user.Age = 31;
users.Update(user);

// Delete a document
users.Delete(user.Id);
```

### 6. Create indexes

```csharp
// Regular index for faster queries
users.CreateIndex(u => u.Email);

// Unique index to enforce uniqueness
users.CreateIndex(u => u.Email, unique: true);
```

### 7. Use transactions

```csharp
using var transaction = db.BeginTransaction();

var users = transaction.GetCollection<User>("users");
var orders = transaction.GetCollection<Order>("orders");

users.Insert(newUser);
orders.Insert(newOrder);

transaction.Commit();  // Atomically commit both operations
```

## Advanced Usage

### In-Memory Databases

Perfect for testing:

```csharp
using var db = SqliteDocumentDatabase.CreateInMemory();
var users = db.GetCollection<User>("users");
// Use as normal - data is kept in memory
```

### Working with DocumentId

```csharp
// Parse from string
var id = DocumentId.Parse("507f1f77bcf86cd799439011");

// Get timestamp
DateTime timestamp = id.Timestamp;

// Generate new ID
var newId = DocumentId.NewId();

// Convert to string
string idString = id.ToString();
```

### Collection Management

```csharp
// List all collections
var collections = db.GetCollectionNames();

// Drop a collection
db.DropCollection("users");

// Check if collection exists
bool exists = db.GetCollectionNames().Contains("users");
```

## Query Translation

LINQ expressions are automatically translated to SQLite JSON queries:

| C# Expression | SQLite Translation |
|--------------|-------------------|
| `u => u.Name == "Alice"` | `json_extract(data, '$.name') = 'Alice'` |
| `u => u.Age > 30` | `json_extract(data, '$.age') > 30` |
| `u => u.Address.City == "Seattle"` | `json_extract(data, '$.address.city') = 'Seattle'` |
| `u => u.Email.Contains("@gmail")` | `json_extract(data, '$.email') LIKE '%@gmail%'` |
| `u => u.Name.StartsWith("A")` | `json_extract(data, '$.name') LIKE 'A%'` |

## Development

Build the solution:
```bash
dotnet build
```

Run tests:
```bash
dotnet test
```

Run the sample application:
```bash
dotnet run --project samples/SampleApp/SampleApp.csproj
```

Create NuGet package:
```bash
dotnet pack -c Release
```

## Architecture

### Database Schema

The library uses four main SQLite tables:
- **collections**: Collection metadata
- **documents**: JSON document storage
- **indexes**: Index definitions
- **indexed_values**: Extracted values for indexed properties

All tables use cascading deletes to maintain referential integrity.

### DocumentId Format

12-byte unique identifier:
- 4 bytes: Unix timestamp (enables chronological sorting)
- 5 bytes: Random value
- 3 bytes: Incrementing counter

This provides temporal ordering and uniqueness without database round-trips.

## Requirements

- .NET Standard 2.0 or higher
- SQLite 3.9.0 or higher (for JSON support)

## Dependencies

- Microsoft.Data.Sqlite
- Dapper
- System.Text.Json
- Microsoft.Extensions.Logging.Abstractions
- Polly
