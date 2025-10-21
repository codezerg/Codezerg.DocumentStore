using Codezerg.DocumentStore.Caching;
using Codezerg.DocumentStore.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codezerg.DocumentStore;

/// <summary>
/// SQLite-backed document database implementation.
/// </summary>
public class SqliteDocumentDatabase : IDocumentDatabase
{
    private readonly ISqliteConnectionProvider _connectionProvider;
    private readonly DocumentDatabaseOptions _options;
    private readonly ILogger<SqliteDocumentDatabase> _logger;
    private readonly ConcurrentDictionary<string, object> _collections = new();
    private bool _supportsJsonB;
    private bool _schemaInitialized;
    private readonly object _schemaLock = new object();

    /// <summary>
    /// Gets whether to use JSONB (binary JSON) storage format.
    /// </summary>
    public bool UseJsonB => _options.UseJsonB;


    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDocumentDatabase"/> class using options pattern.
    /// </summary>
    public SqliteDocumentDatabase(ISqliteConnectionProvider connectionProvider,
        IOptions<DocumentDatabaseOptions> options,
        ILogger<SqliteDocumentDatabase>? logger = null)
    {
        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<SqliteDocumentDatabase>.Instance;

        // Verify JSON support and detect JSONB with a temporary connection
        EnableJsonSupport();

        // Initialize schema on first connection
        EnsureSchemaInitialized();
    }

    internal DbConnection CreateConnection()
    {
        return _connectionProvider.CreateConnection();
    }

    /// <inheritdoc/>
    public async Task<IDocumentCollection<T>> GetCollectionAsync<T>(string name) where T : class, IDocument
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        var collection = (IDocumentCollection<T>)_collections.GetOrAdd(name, _ =>
        {
            var baseCollection = new SqliteDocumentCollection<T>(this, name, _logger, _supportsJsonB);

            var useCache = _options.CachedCollectionPredicates.Any(x => x(name));
            if (useCache)
                return new CachedDocumentCollection<T>(baseCollection);

            return baseCollection;
        });

        await CreateCollectionInternalAsync<T>(name);

        return collection;
    }

    /// <inheritdoc/>
    public async Task CreateCollectionAsync<T>(string name) where T : class, IDocument
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        var now = DateTime.UtcNow.ToString("O");

        // Insert into collections table if not exists
        var insertSql = @"
            INSERT OR IGNORE INTO collections (name, created_at)
            VALUES (@Name, @CreatedAt);";

        using (var connection = _connectionProvider.CreateConnection())
        {
            await connection.ExecuteAsync(insertSql, new { Name = name, CreatedAt = now });
        }

        _logger.LogInformation("Created collection {CollectionName}", name);
    }

    /// <inheritdoc/>
    public async Task DropCollectionAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        // Delete from collections table (cascade will handle documents, indexes, and indexed_values)
        var deleteSql = "DELETE FROM collections WHERE name = @Name;";

        using (var connection = _connectionProvider.CreateConnection())
        {
            await connection.ExecuteAsync(deleteSql, new { Name = name });
        }

        _collections.TryRemove(name, out _);

        _logger.LogInformation("Dropped collection {CollectionName}", name);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ListCollectionNamesAsync()
    {
        var sql = "SELECT name FROM collections ORDER BY name;";

        using (var connection = _connectionProvider.CreateConnection())
        {
            var collectionNames = await connection.QueryAsync<string>(sql);
            return new List<string>(collectionNames);
        }
    }

    private async Task CreateCollectionInternalAsync<T>(string name) where T : class, IDocument
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        var now = DateTime.UtcNow.ToString("O");

        // Insert into collections table if not exists
        var insertSql = @"
            INSERT OR IGNORE INTO collections (name, created_at)
            VALUES (@Name, @CreatedAt);";

        using (var connection = _connectionProvider.CreateConnection())
        {
            await connection.ExecuteAsync(insertSql, new { Name = name, CreatedAt = now });
        }

        _logger.LogInformation("Created collection {CollectionName}", name);
    }

    private void EnsureSchemaInitialized()
    {
        if (_schemaInitialized)
            return;

        lock (_schemaLock)
        {
            if (_schemaInitialized)
                return;

            using (var connection = _connectionProvider.CreateConnection())
            {
                InitializeSchema(connection);
            }

            _schemaInitialized = true;
        }
    }

    private void InitializeSchema(DbConnection connection)
    {
        // Create collections table
        var createCollectionsTable = @"
            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL
            );";

        // Create documents table with appropriate data type
        var dataType = _supportsJsonB ? "BLOB" : "TEXT";
        var createDocumentsTable = $@"
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                document_id TEXT NOT NULL,
                data {dataType} NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                version INTEGER DEFAULT 1,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
                UNIQUE(collection_id, document_id)
            );";

        // Create indexes table
        var createIndexesTable = @"
            CREATE TABLE IF NOT EXISTS indexes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                fields TEXT NOT NULL,
                unique_index INTEGER DEFAULT 0,
                sparse INTEGER DEFAULT 0,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
                UNIQUE(collection_id, name)
            );";

        // Create indexed_values table
        var createIndexedValuesTable = @"
            CREATE TABLE IF NOT EXISTS indexed_values (
                document_id INTEGER NOT NULL,
                index_id INTEGER NOT NULL,
                field_path TEXT NOT NULL,
                value_text TEXT,
                value_number REAL,
                value_boolean INTEGER,
                value_type TEXT,
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
                FOREIGN KEY (index_id) REFERENCES indexes(id) ON DELETE CASCADE
            );";

        // Create performance indexes
        var createIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_documents_collection ON documents(collection_id);
            CREATE INDEX IF NOT EXISTS idx_documents_lookup ON documents(collection_id, document_id);
            CREATE INDEX IF NOT EXISTS idx_indexed_values_lookup ON indexed_values(index_id, field_path, value_text);
            CREATE INDEX IF NOT EXISTS idx_indexed_values_number ON indexed_values(index_id, field_path, value_number);";

        connection.Execute(createCollectionsTable);
        connection.Execute(createDocumentsTable);
        connection.Execute(createIndexesTable);
        connection.Execute(createIndexedValuesTable);
        connection.Execute(createIndexes);

        _logger.LogDebug("Centralized schema initialized");
    }

    private void EnableJsonSupport()
    {
        using var connection = _connectionProvider.CreateConnection();

        // SQLite has built-in JSON support, no additional setup needed
        // Just verify it's available
        var result = connection.QuerySingle<int>("SELECT json_valid('{\"test\": true}');");
        if (result != 1)
        {
            throw new InvalidOperationException("SQLite JSON support is not available.");
        }

        // Set JSONB support based on user preference
        _supportsJsonB = UseJsonB;

        if (_supportsJsonB)
        {
            _logger.LogInformation("JSONB storage enabled");
        }
        else
        {
            _logger.LogDebug("JSON text storage enabled");
        }
    }
}
