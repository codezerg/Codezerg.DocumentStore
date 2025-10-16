using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Codezerg.DocumentStore;

/// <summary>
/// SQLite-backed document database implementation.
/// </summary>
public class SqliteDocumentDatabase : IDocumentDatabase
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly SqliteConnection _connection;
    private readonly ConcurrentDictionary<string, object> _collections = new();
    private readonly ILogger<SqliteDocumentDatabase> _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName => _databaseName;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDocumentDatabase"/> class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="logger">Optional logger.</param>
    public SqliteDocumentDatabase(string connectionString, ILogger<SqliteDocumentDatabase>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _connectionString = connectionString;
        _logger = logger ?? NullLogger<SqliteDocumentDatabase>.Instance;

        // Extract database name from connection string or file path
        var builder = new SqliteConnectionStringBuilder(connectionString);
        _databaseName = string.IsNullOrEmpty(builder.DataSource)
            ? "memory"
            : Path.GetFileNameWithoutExtension(builder.DataSource);

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        _logger.LogInformation("Opened database connection to {DatabaseName}", _databaseName);

        // Enable JSON support
        EnableJsonSupport();

        // Initialize centralized schema
        InitializeSchema();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteDocumentDatabase"/> class using options pattern.
    /// </summary>
    /// <param name="options">The database configuration options.</param>
    /// <param name="logger">Optional logger.</param>
    public SqliteDocumentDatabase(IOptions<Configuration.DocumentDatabaseOptions> options, ILogger<SqliteDocumentDatabase>? logger = null)
        : this(GetConnectionStringFromOptions(options), logger)
    {
    }

    private static string GetConnectionStringFromOptions(IOptions<Configuration.DocumentDatabaseOptions> options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var optionsValue = options.Value;
        if (optionsValue == null)
            throw new ArgumentException("Options value cannot be null.", nameof(options));

        optionsValue.Validate();
        return optionsValue.ConnectionString!;
    }

    /// <inheritdoc/>
    public IDocumentCollection<T> GetCollection<T>(string name) where T : class
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        return (IDocumentCollection<T>)_collections.GetOrAdd(name, _ =>
        {
            var collection = new SqliteDocumentCollection<T>(this, name, _logger);
            CreateCollection<T>(name);
            return collection;
        });
    }

    /// <inheritdoc/>
    public void CreateCollection<T>(string name) where T : class
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        var now = DateTime.UtcNow.ToString("O");

        // Insert into collections table if not exists
        var insertSql = @"
            INSERT OR IGNORE INTO collections (name, created_at)
            VALUES (@Name, @CreatedAt);";

        _connection.Execute(insertSql, new { Name = name, CreatedAt = now });

        _logger.LogInformation("Created collection {CollectionName}", name);
    }

    /// <inheritdoc/>
    public void DropCollection(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or empty.", nameof(name));

        // Delete from collections table (cascade will handle documents, indexes, and indexed_values)
        var deleteSql = "DELETE FROM collections WHERE name = @Name;";
        _connection.Execute(deleteSql, new { Name = name });

        _collections.TryRemove(name, out _);

        _logger.LogInformation("Dropped collection {CollectionName}", name);
    }

    /// <inheritdoc/>
    public List<string> ListCollectionNames()
    {
        var sql = "SELECT name FROM collections ORDER BY name;";
        var collectionNames = _connection.Query<string>(sql);
        return new List<string>(collectionNames);
    }

    /// <inheritdoc/>
    public IDocumentTransaction BeginTransaction()
    {
        var transaction = _connection.BeginTransaction();
        _logger.LogDebug("Transaction started");
        return new SqliteDocumentTransaction(transaction);
    }

    /// <summary>
    /// Gets the underlying SQLite connection (internal use).
    /// </summary>
    internal SqliteConnection GetConnection() => _connection;

    private void InitializeSchema()
    {
        // Create collections table
        var createCollectionsTable = @"
            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at TEXT NOT NULL
            );";

        // Create documents table
        var createDocumentsTable = @"
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                document_id TEXT NOT NULL,
                data TEXT NOT NULL,
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

        _connection.Execute(createCollectionsTable);
        _connection.Execute(createDocumentsTable);
        _connection.Execute(createIndexesTable);
        _connection.Execute(createIndexedValuesTable);
        _connection.Execute(createIndexes);

        _logger.LogDebug("Centralized schema initialized");
    }

    private void EnableJsonSupport()
    {
        // SQLite has built-in JSON support, no additional setup needed
        // Just verify it's available
        var result = _connection.QuerySingle<int>("SELECT json_valid('{\"test\": true}');");
        if (result != 1)
        {
            throw new InvalidOperationException("SQLite JSON support is not available.");
        }

        _logger.LogDebug("JSON support enabled");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _connection?.Dispose();
        _disposed = true;

        _logger.LogInformation("Database connection closed");

        GC.SuppressFinalize(this);
    }
}
