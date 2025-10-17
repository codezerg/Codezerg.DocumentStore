using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a database transaction.
/// </summary>
internal class SqliteDocumentTransaction : IDocumentTransaction, IAsyncDisposable
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

    public Task CommitAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteDocumentTransaction));

        if (_completed)
            throw new InvalidOperationException("Transaction has already been completed.");

        _transaction.Commit();
        _completed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteDocumentTransaction));

        if (_completed)
            throw new InvalidOperationException("Transaction has already been completed.");

        _transaction.Rollback();
        _completed = true;
        return Task.CompletedTask;
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

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
