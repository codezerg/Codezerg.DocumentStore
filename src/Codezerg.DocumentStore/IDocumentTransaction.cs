using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a database transaction.
/// </summary>
public interface IDocumentTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the underlying database transaction.
    /// </summary>
    IDbTransaction DbTransaction { get; }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    Task CommitAsync();

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    Task RollbackAsync();
}
