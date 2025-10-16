using Microsoft.Data.Sqlite;
using System;
using System.Data;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a database transaction.
/// </summary>
internal class SqliteDocumentTransaction : IDocumentTransaction
{
    private readonly SqliteTransaction _transaction;
    private bool _disposed;
    private bool _completed;

    internal SqliteTransaction SqliteTransaction => _transaction;

    public IDbTransaction DbTransaction => _transaction;

    public SqliteDocumentTransaction(SqliteTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteDocumentTransaction));

        if (_completed)
            throw new InvalidOperationException("Transaction has already been completed.");

        _transaction.Commit();
        _completed = true;
    }

    public void Rollback()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteDocumentTransaction));

        if (_completed)
            throw new InvalidOperationException("Transaction has already been completed.");

        _transaction.Rollback();
        _completed = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (!_completed)
        {
            try
            {
                _transaction.Rollback();
            }
            catch
            {
                // Ignore errors during rollback on dispose
            }
        }

        _transaction.Dispose();
        _disposed = true;
    }
}
