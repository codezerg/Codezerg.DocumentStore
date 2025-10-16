using System;

namespace Codezerg.DocumentStore.Configuration;

/// <summary>
/// Configuration options for <see cref="IDocumentDatabase"/>.
/// </summary>
public class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets the connection string for the SQLite database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Validates the options.
    /// </summary>
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "DocumentDatabaseOptions must specify a ConnectionString.");
        }
    }
}