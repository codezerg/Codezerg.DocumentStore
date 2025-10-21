using System;
using System.Collections.Generic;

namespace Codezerg.DocumentStore.Configuration;

/// <summary>
/// Configuration options for <see cref="IDocumentDatabase"/>.
/// </summary>
public class DocumentDatabaseOptions
{
    /// <summary>
    /// Gets or sets whether to use JSONB (binary JSON) storage format.
    /// JSONB provides significant performance improvements (20-76% faster operations) with minimal storage overhead.
    /// Default is true (recommended). Set to false for legacy JSON text storage.
    /// </summary>
    public bool UseJsonB { get; set; } = true;

    /// <summary>
    /// List of predicate functions that determine if a collection should be cached
    /// </summary>
    internal List<Func<string, bool>> CachedCollectionPredicates { get; set; } = new();

    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
    }
}