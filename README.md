# Codezerg.DocumentStore

Document-oriented data layer for SQLite. Store and query JSON documents with LINQ support.

## Features

- **Document storage**: Store POCOs as JSON documents without predefined schemas
- **LINQ queries**: Type-safe queries translated to SQLite JSON operations
- **Indexing**: Regular and unique indexes for fast lookups
- **Transactions**: ACID transaction support across collections
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

users.Insert(user);  // Id is auto-assigned
```

### 4. Query documents

```csharp
var user = users.FindById(userId);
var adults = users.Find(u => u.Age >= 18);
var seattleUsers = users.Find(u => u.Address.City == "Seattle");
var gmailUsers = users.Find(u => u.Email.Contains("@gmail.com"));
var results = users.Find(u => u.Age > 25 && u.Address.State == "WA");
var page = users.Find(u => u.Age > 0, skip: 10, limit: 20);
var allUsers = users.FindAll();
```

### 5. Update and delete

```csharp
user.Age = 31;
users.Update(user);

users.Delete(user.Id);
```

### 6. Create indexes

```csharp
users.CreateIndex(u => u.Email);
users.CreateIndex(u => u.Email, unique: true);
```

### 7. Use transactions

```csharp
using var transaction = db.BeginTransaction();

var users = transaction.GetCollection<User>("users");
var orders = transaction.GetCollection<Order>("orders");

users.Insert(newUser);
orders.Insert(newOrder);

transaction.Commit();
```

## Advanced Usage

### In-Memory Databases

```csharp
using var db = new SqliteDocumentDatabase("Data Source=:memory:");
var users = db.GetCollection<User>("users");
```

### Dependency Injection

```csharp
services.AddDocumentDatabase(options =>
    options.UseConnectionString("Data Source=myapp.db"));

public class MyService
{
    private readonly IDocumentDatabase _database;

    public MyService(IDocumentDatabase database)
    {
        _database = database;
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
var collections = db.GetCollectionNames();
db.DropCollection("users");
bool exists = db.GetCollectionNames().Contains("users");
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
dotnet build
dotnet test
dotnet run --project samples/SampleApp/SampleApp.csproj
dotnet pack -c Release
```

## Architecture

### DocumentId Format

12-byte unique identifier (4-byte timestamp + 5-byte random + 3-byte counter) providing temporal ordering and uniqueness without database round-trips.

### Database Schema

Four tables: `collections`, `documents`, `indexes`, `indexed_values` with cascading deletes.

## License

MIT
