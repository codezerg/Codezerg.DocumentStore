using Codezerg.DocumentStore.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Data.Common;
using System.IO;

namespace Codezerg.DocumentStore;

/// <summary>
/// Provides SQLite database connections using ADO.NET provider factories.
/// Supports both Microsoft.Data.Sqlite and System.Data.SQLite providers.
/// </summary>
public class SqliteConnectionProvider : ISqliteConnectionProvider
{
    private readonly DbProviderFactory _providerFactory;
    private readonly SqliteDatabaseOptions _options;
    private readonly ILogger<SqliteConnectionProvider>? _logger;
    private readonly string _databaseName;
    private readonly string _connectionString;
    private readonly string _providerName;

    /// <inheritdoc/>
    public string DatabaseName => _databaseName;

    /// <inheritdoc/>
    public string ConnectionString => _connectionString;

    /// <inheritdoc/>
    public string ProviderName => _providerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConnectionProvider"/> class.
    /// </summary>
    /// <param name="options">The database configuration options.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public SqliteConnectionProvider(IOptions<SqliteDatabaseOptions> options, ILogger<SqliteConnectionProvider>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<SqliteConnectionProvider>.Instance;

        _connectionString = _options.ConnectionString;
        _providerName = _options.ProviderName;
        _databaseName = ExtractDatabaseName(_connectionString);

        _providerFactory = DbProviderFactories.GetFactory(_providerName);
        if (_providerFactory == null)
            throw new InvalidOperationException($"Failed to get provider factory for provider name '{_options.ProviderName}'.");
    }

    /// <summary>
    /// Creates a new database connection for the current operation (internal use).
    /// </summary>
    public DbConnection CreateConnection()
    {
        var connection = _providerFactory.CreateConnection();
        if (connection == null)
            throw new InvalidOperationException($"Failed to create connection from provider '{_options.ProviderName}'.");

        connection.ConnectionString = _connectionString;
        connection.Open();

        // Apply pragmas to each connection
        ApplyPragmas(connection);

        return connection;
    }

    private void ApplyPragmas(DbConnection connection)
    {
        // Apply journal mode
        var journalMode = _options.JournalMode;
        if (!string.IsNullOrWhiteSpace(journalMode))
        {
            var journalSql = $"PRAGMA journal_mode = {journalMode};";
            connection.Execute(journalSql);
            _logger?.LogDebug("Applied PRAGMA journal_mode = {JournalMode}", journalMode);
        }

        // Apply page size (must be set before any tables are created)
        var pageSize = _options.PageSize;
        if (pageSize.HasValue)
        {
            var pageSizeSql = $"PRAGMA page_size = {pageSize.Value};";
            connection.Execute(pageSizeSql);
            _logger?.LogDebug("Applied PRAGMA page_size = {PageSize}", pageSize.Value);
        }

        // Apply synchronous mode
        var synchronous = _options.Synchronous;
        if (!string.IsNullOrWhiteSpace(synchronous))
        {
            var syncSql = $"PRAGMA synchronous = {synchronous};";
            connection.Execute(syncSql);
            _logger?.LogDebug("Applied PRAGMA synchronous = {Synchronous}", synchronous);
        }
    }


    /// <summary>
    /// Extracts the database name from a connection string.
    /// </summary>
    private static string ExtractDatabaseName(string connectionString)
    {
        // Try to extract Data Source from connection string
        var dataSourceKey = "Data Source=";
        var idx = connectionString.IndexOf(dataSourceKey, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var start = idx + dataSourceKey.Length;
            var end = connectionString.IndexOf(';', start);
            var dataSource = end >= 0
                ? connectionString.Substring(start, end - start).Trim()
                : connectionString.Substring(start).Trim();

            // Handle :memory: databases
            if (string.IsNullOrEmpty(dataSource) || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
                return "memory";

            // Extract filename without extension
            return Path.GetFileNameWithoutExtension(dataSource);
        }

        // Fallback to generic name if Data Source not found
        return "database";
    }

}
