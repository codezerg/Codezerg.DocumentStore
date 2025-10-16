using Microsoft.Data.Sqlite;
using System;
using System.Data;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a database transaction.
/// </summary>
public interface IDocumentTransaction : IDisposable
{
    /// <summary>
    /// Gets the underlying database transaction.
    /// </summary>
    IDbTransaction DbTransaction { get; }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    void Rollback();
}
