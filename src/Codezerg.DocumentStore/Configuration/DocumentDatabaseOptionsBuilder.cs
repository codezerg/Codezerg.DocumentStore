using System;

namespace Codezerg.DocumentStore.Configuration;

/// <summary>
/// Builder for configuring <see cref="DocumentDatabaseOptions"/> with a fluent API.
/// </summary>
public class DocumentDatabaseOptionsBuilder
{
    private readonly DocumentDatabaseOptions _options = new();

    /// <summary>
    /// Configures the database to use JSONB (binary JSON) storage format.
    /// JSONB provides significant performance improvements (20-60% faster operations) with 5-10% storage savings.
    /// </summary>
    /// <param name="useJsonB">True to enable JSONB storage, false for JSON text storage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder UseJsonB(bool useJsonB = true)
    {
        _options.UseJsonB = useJsonB;
        return this;
    }

    /// <summary>
    /// Marks a collection to be cached in memory for faster access.
    /// </summary>
    /// <param name="collectionName">The name of the collection to cache.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder CacheCollection(string collectionName)
    {
        _options.CachedCollectionPredicates.Add(name =>
            string.Equals(name, collectionName, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    /// <summary>
    /// Configures which collections should be cached using a predicate function.
    /// </summary>
    /// <param name="predicate">A function that determines if a collection should be cached based on its name.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public DocumentDatabaseOptionsBuilder CacheCollections(Func<string, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        _options.CachedCollectionPredicates.Add(predicate);
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
