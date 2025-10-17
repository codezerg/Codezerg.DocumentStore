using System;
using System.Collections.Generic;

namespace Codezerg.DocumentStore.Configuration;

/// <summary>
/// Configuration options for <see cref="IDocumentDatabase"/>.
/// </summary>
public class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets the connection string for the SQLite database.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether to use JSONB (binary JSON) storage format.
    /// JSONB provides significant performance improvements (20-76% faster operations) with minimal storage overhead.
    /// Default is true (recommended). Set to false for legacy JSON text storage.
    /// </summary>
    public bool UseJsonB { get; set; } = true;

    /// <summary>
    /// Gets or sets the SQLite journal mode (e.g., WAL, DELETE, TRUNCATE, PERSIST, MEMORY, OFF).
    /// WAL (Write-Ahead Logging) is recommended for better concurrency.
    /// Default is "WAL".
    /// </summary>
    public string? JournalMode { get; set; } = "WAL";

    /// <summary>
    /// Gets or sets the SQLite page size in bytes.
    /// Common values are 1024, 2048, 4096, 8192, 16384, 32768, or 65536.
    /// Default is 4096. Set to null to use SQLite's default.
    /// </summary>
    public int? PageSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the SQLite synchronous mode (e.g., FULL, NORMAL, OFF).
    /// NORMAL provides good balance between safety and performance.
    /// Default is "NORMAL".
    /// </summary>
    public string? Synchronous { get; set; } = "NORMAL";

    /// <summary>
    /// List of predicate functions that determine if a collection should be cached
    /// </summary>
    internal List<Func<string, bool>> CachedCollectionPredicates { get; set; } = new();

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

        // Validate page size if specified
        if (PageSize.HasValue)
        {
            var validPageSizes = new[] { 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536 };
            if (Array.IndexOf(validPageSizes, PageSize.Value) == -1)
            {
                throw new InvalidOperationException(
                    $"PageSize must be one of: {string.Join(", ", validPageSizes)}");
            }
        }
    }
}