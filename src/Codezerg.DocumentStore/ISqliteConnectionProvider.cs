using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace Codezerg.DocumentStore;

/// <summary>
/// Provides SQLite database connections using ADO.NET provider factories.
/// </summary>
public interface ISqliteConnectionProvider
{
    /// <summary>
    /// Gets the ADO.NET provider name (e.g., "Microsoft.Data.Sqlite" or "System.Data.SQLite").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the database name extracted from the connection string.
    /// </summary>
    string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string used for database connections.
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// Creates and opens a new database connection with configured pragmas applied.
    /// </summary>
    /// <returns>An open database connection.</returns>
    DbConnection CreateConnection();
}
