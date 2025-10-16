using Codezerg.DocumentStore;

namespace SampleApp;

/// <summary>
/// Sample application demonstrating Codezerg.DocumentStore usage
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Codezerg.DocumentStore Sample Application ===\n");

        // Create or open a database
        using var database = SqliteDocumentDatabase.Create("sample.db");

        // Get a collection of User documents
        var users = database.GetCollection<User>("users");
        var orders = database.GetCollection<Order>("orders");

        // Clear existing data for demo
        users.DeleteMany(u => u.Age > 0);
        orders.DeleteMany(o => o.Total > 0);

        Console.WriteLine("1. Basic CRUD Operations");
        Console.WriteLine("========================\n");

        DemoBasicCrud(users);

        Console.WriteLine("\n2. Query Operations");
        Console.WriteLine("===================\n");

        DemoQueries(users);

        Console.WriteLine("\n3. Update Operations");
        Console.WriteLine("====================\n");

        DemoUpdates(users);

        Console.WriteLine("\n4. Transaction Support");
        Console.WriteLine("======================\n");

        DemoTransactions(database, users, orders);

        Console.WriteLine("\n5. Index Management");
        Console.WriteLine("===================\n");

        DemoIndexes(users);

        Console.WriteLine("\n6. Complex Queries");
        Console.WriteLine("==================\n");

        DemoComplexQueries(users);

        Console.WriteLine("\n=== Sample Application Complete ===");
    }

    static void DemoBasicCrud(IDocumentCollection<User> users)
    {
        // Insert a single document
        var user1 = new User
        {
            Name = "Alice Johnson",
            Email = "alice@example.com",
            Age = 28,
            City = "New York",
            Tags = new List<string> { "developer", "team-lead" }
        };

        users.InsertOne(user1);
        Console.WriteLine($"✓ Inserted user: {user1.Name} (ID: {user1.Id})");

        // Insert multiple documents
        var newUsers = new[]
        {
            new User { Name = "Bob Smith", Email = "bob@example.com", Age = 35, City = "San Francisco", Tags = new List<string> { "developer", "senior" } },
            new User { Name = "Carol White", Email = "carol@example.com", Age = 42, City = "Seattle", Tags = new List<string> { "manager", "architect" } },
            new User { Name = "David Brown", Email = "david@example.com", Age = 28, City = "Austin", Tags = new List<string> { "developer", "junior" } },
            new User { Name = "Eve Davis", Email = "eve@example.com", Age = 31, City = "New York", Tags = new List<string> { "designer", "ux" } }
        };

        users.InsertMany(newUsers);
        Console.WriteLine($"✓ Inserted {newUsers.Length} users");

        // Count documents
        var totalUsers = users.CountAll();
        Console.WriteLine($"✓ Total users in collection: {totalUsers}");

        // Find by ID
        var foundUser = users.FindById(user1.Id);
        Console.WriteLine($"✓ Found user by ID: {foundUser?.Name}");
    }

    static void DemoQueries(IDocumentCollection<User> users)
    {
        // Find all users
        var allUsers = users.FindAll();
        Console.WriteLine($"✓ Found {allUsers.Count} total users");

        // Find users by age
        var youngUsers = users.Find(u => u.Age < 30);
        Console.WriteLine($"✓ Found {youngUsers.Count} users under 30:");
        foreach (var user in youngUsers)
        {
            Console.WriteLine($"  - {user.Name} (Age: {user.Age})");
        }

        // Find users by city
        var nyUsers = users.Find(u => u.City == "New York");
        Console.WriteLine($"✓ Found {nyUsers.Count} users in New York");

        // Find one user
        var developer = users.FindOne(u => u.Name == "Bob Smith");
        Console.WriteLine($"✓ Found developer: {developer?.Name}, Email: {developer?.Email}");

        // Count with filter
        var developerCount = users.Count(u => u.Age > 30);
        Console.WriteLine($"✓ Users over 30: {developerCount}");

        // Check if any user exists
        var hasManagers = users.Any(u => u.Age > 40);
        Console.WriteLine($"✓ Has users over 40: {hasManagers}");
    }

    static void DemoUpdates(IDocumentCollection<User> users)
    {
        // Find a user to update
        var user = users.FindOne(u => u.Name == "Alice Johnson");

        if (user != null)
        {
            // Update the user
            user.Age = 29;
            user.City = "Boston";

            var updated = users.UpdateById(user.Id, user);
            Console.WriteLine($"✓ Updated user {user.Name}: Age={user.Age}, City={user.City}");
        }

        // Update multiple users
        var updateCount = users.UpdateMany(
            u => u.Age < 30,
            user => user.Tags?.Add("young-professional")
        );
        Console.WriteLine($"✓ Updated {updateCount} young users with 'young-professional' tag");

        // Delete a user
        var toDelete = users.FindOne(u => u.Name == "David Brown");
        if (toDelete != null)
        {
            users.DeleteById(toDelete.Id);
            Console.WriteLine($"✓ Deleted user: {toDelete.Name}");
        }

        // Delete multiple users
        var deleteCount = users.DeleteMany(u => u.Age > 50);
        Console.WriteLine($"✓ Deleted {deleteCount} users over 50");
    }

    static void DemoTransactions(
        IDocumentDatabase database,
        IDocumentCollection<User> users,
        IDocumentCollection<Order> orders)
    {
        Console.WriteLine("Creating user and order in transaction...");

        using var transaction = database.BeginTransaction();

        try
        {
            // Create a new user
            var newUser = new User
            {
                Name = "Frank Wilson",
                Email = "frank@example.com",
                Age = 33,
                City = "Chicago",
                Tags = new List<string> { "customer" }
            };

            users.InsertOne(newUser, transaction);

            // Create an order for that user
            var order = new Order
            {
                UserId = newUser.Id,
                OrderNumber = "ORD-2024-001",
                Total = 299.99m,
                Items = new List<string> { "Laptop", "Mouse", "Keyboard" }
            };

            orders.InsertOne(order, transaction);

            // Commit the transaction
            transaction.Commit();
            Console.WriteLine($"✓ Transaction committed: User '{newUser.Name}' and Order '{order.OrderNumber}'");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"✗ Transaction rolled back: {ex.Message}");
        }

        // Demonstrate rollback
        Console.WriteLine("\nDemonstrating transaction rollback...");

        using (var tx = database.BeginTransaction())
        {
            var testUser = new User
            {
                Name = "Test User",
                Email = "test@example.com",
                Age = 25,
                City = "Test City"
            };

            users.InsertOne(testUser, tx);
            Console.WriteLine("✓ User inserted in transaction");

            // Transaction disposed without commit = automatic rollback
        }

        var rolledBackUser = users.FindOne(u => u.Name == "Test User");
        Console.WriteLine($"✓ User after rollback: {(rolledBackUser == null ? "Not found (rolled back)" : "Found (ERROR)")}");
    }

    static void DemoIndexes(IDocumentCollection<User> users)
    {
        // Create an index on Email field
        users.CreateIndex(u => u.Email, unique: true);
        Console.WriteLine("✓ Created unique index on Email field");

        // Create an index on City field
        users.CreateIndex(u => u.City);
        Console.WriteLine("✓ Created index on City field");

        // Queries using indexes will be faster
        var usersInNY = users.Find(u => u.City == "New York");
        Console.WriteLine($"✓ Query with index found {usersInNY.Count} users");
    }

    static void DemoComplexQueries(IDocumentCollection<User> users)
    {
        // Add more test data
        users.InsertMany(new[]
        {
            new User { Name = "Grace Lee", Email = "grace@example.com", Age = 26, City = "Los Angeles", Tags = new List<string> { "developer", "frontend" } },
            new User { Name = "Henry Zhang", Email = "henry@example.com", Age = 38, City = "San Francisco", Tags = new List<string> { "developer", "backend" } },
            new User { Name = "Iris Chen", Email = "iris@example.com", Age = 29, City = "Seattle", Tags = new List<string> { "developer", "fullstack" } }
        });

        // Complex query with multiple conditions
        var experiencedDevs = users.Find(u =>
            u.Age >= 30 && u.City == "San Francisco");
        Console.WriteLine($"✓ Experienced developers in SF: {experiencedDevs.Count}");

        // String contains query
        var emailContains = users.Find(u => u.Email!.Contains("example.com"));
        Console.WriteLine($"✓ Users with 'example.com' email: {emailContains.Count}");

        // Pagination
        var page1 = users.Find(u => u.Age > 0, skip: 0, limit: 3);
        Console.WriteLine($"✓ Page 1 (first 3 users): {string.Join(", ", page1.Select(u => u.Name))}");

        var page2 = users.Find(u => u.Age > 0, skip: 3, limit: 3);
        Console.WriteLine($"✓ Page 2 (next 3 users): {string.Join(", ", page2.Select(u => u.Name))}");

        // Get all developers
        var allDevs = users.FindAll();
        Console.WriteLine($"\n✓ All users in database: {allDevs.Count}");
        foreach (var user in allDevs.OrderBy(u => u.Name))
        {
            Console.WriteLine($"  - {user.Name} ({user.Age}) - {user.City} - [{string.Join(", ", user.Tags ?? new List<string>())}]");
        }
    }
}

// Sample document classes
public class User
{
    public DocumentId Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
    public string? City { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Order
{
    public DocumentId Id { get; set; }
    public DocumentId UserId { get; set; }
    public string? OrderNumber { get; set; }
    public decimal Total { get; set; }
    public List<string>? Items { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
