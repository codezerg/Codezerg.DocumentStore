using Codezerg.DocumentStore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SystemDataSQLiteTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Testing System.Data.SQLite Provider ===\n");

        var dbFile = Path.Combine(Path.GetTempPath(), $"test_systemdata_{Guid.NewGuid()}.db");

        try
        {
            // Create database using System.Data.SQLite provider
            // Note: We need to use the options pattern to specify a different provider
            var options = Microsoft.Extensions.Options.Options.Create(new Codezerg.DocumentStore.Configuration.DocumentDatabaseOptions
            {
                ProviderName = "System.Data.SQLite",
                ConnectionString = $"Data Source={dbFile}",
                UseJsonB = true
            });

            using var db = new SqliteDocumentDatabase(options);

            Console.WriteLine($"✓ Database created with provider: System.Data.SQLite");
            Console.WriteLine($"  Database name: {db.DatabaseName}");
            Console.WriteLine($"  Connection string: {db.ConnectionString}");
            Console.WriteLine($"  Using JSONB: {db.UseJsonB}\n");

            // Get a collection
            var users = await db.GetCollectionAsync<User>("users");
            Console.WriteLine("✓ Collection 'users' created\n");

            // Insert a user
            var user = new User
            {
                Name = "John Doe",
                Email = "john@example.com",
                Age = 30
            };

            await users.InsertOneAsync(user);
            Console.WriteLine($"✓ Inserted user: {user.Name} (ID: {user.Id})\n");

            // Find the user
            var foundUser = await users.FindByIdAsync(user.Id);
            if (foundUser != null)
            {
                Console.WriteLine($"✓ Found user by ID: {foundUser.Name}, Age: {foundUser.Age}\n");
            }

            // Query users
            await users.InsertManyAsync(new[]
            {
                new User { Name = "Alice", Email = "alice@example.com", Age = 25 },
                new User { Name = "Bob", Email = "bob@example.com", Age = 35 }
            });

            var allUsers = await users.FindAllAsync();
            Console.WriteLine($"✓ Total users: {allUsers.Count}");
            foreach (var u in allUsers)
            {
                Console.WriteLine($"  - {u.Name} ({u.Age})");
            }
            Console.WriteLine();

            // Query with filter
            var youngUsers = await users.FindAsync(u => u.Age < 30);
            Console.WriteLine($"✓ Users under 30: {youngUsers.Count}");
            foreach (var u in youngUsers)
            {
                Console.WriteLine($"  - {u.Name} ({u.Age})");
            }
            Console.WriteLine();

            // Update
            foundUser!.Age = 31;
            await users.UpdateByIdAsync(foundUser.Id, foundUser);
            Console.WriteLine($"✓ Updated {foundUser.Name}'s age to {foundUser.Age}\n");

            // Delete
            await users.DeleteOneAsync(u => u.Name == "Bob");
            Console.WriteLine("✓ Deleted user 'Bob'\n");

            var finalCount = await users.CountAllAsync();
            Console.WriteLine($"✓ Final user count: {finalCount}\n");

            Console.WriteLine("=== All tests passed with System.Data.SQLite! ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }

            return;
        }
        finally
        {
            // Cleanup
            if (File.Exists(dbFile))
            {
                try { File.Delete(dbFile); } catch { }
            }
        }
    }
}

public class User
{
    public DocumentId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}
