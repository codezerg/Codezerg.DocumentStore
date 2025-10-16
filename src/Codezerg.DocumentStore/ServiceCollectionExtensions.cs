using Codezerg.DocumentStore.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Codezerg.DocumentStore;

/// <summary>
/// Extension methods for configuring document database services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds document database services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="DocumentDatabaseOptions"/>.</param>
    /// <returns>The service collection for fluent chaining.</returns>
    public static IServiceCollection AddDocumentDatabase(
        this IServiceCollection services,
        Action<DocumentDatabaseOptionsBuilder> configureOptions)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        var builder = new DocumentDatabaseOptionsBuilder();
        configureOptions(builder);
        var options = builder.Build();

        // Register options
        services.Configure<DocumentDatabaseOptions>(opt =>
        {
            opt.ConnectionString = options.ConnectionString;
        });

        // Register database as singleton
        services.TryAddSingleton<IDocumentDatabase, SqliteDocumentDatabase>();

        return services;
    }
}
