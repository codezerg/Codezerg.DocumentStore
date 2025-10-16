using System;

namespace Codezerg.DocumentStore.Configuration;

/// <summary>
/// Builder for configuring <see cref="DocumentDatabaseOptions"/> with a fluent API.
/// </summary>
public class DocumentDatabaseOptionsBuilder
{
    private readonly DocumentDatabaseOptions _options = new();

    /// <summary>
    /// Configures the database to use a specific connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        _options.ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Builds the configured options.
    /// </summary>
    /// <returns>The configured <see cref="DocumentDatabaseOptions"/>.</returns>
    internal DocumentDatabaseOptions Build()
    {
        _options.Validate();
        return _options;
    }
}
