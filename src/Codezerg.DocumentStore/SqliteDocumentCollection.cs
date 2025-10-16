using Codezerg.DocumentStore.Exceptions;
using Codezerg.DocumentStore.Serialization;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Codezerg.DocumentStore;

/// <summary>
/// SQLite-backed document collection implementation.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
internal class SqliteDocumentCollection<T> : IDocumentCollection<T> where T : class
{
    private readonly SqliteDocumentDatabase _database;
    private readonly string _collectionName;
    private readonly ILogger _logger;
    private long? _collectionId;

    public string CollectionName => _collectionName;

    public SqliteDocumentCollection(
        SqliteDocumentDatabase database,
        string collectionName,
        ILogger logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger ?? NullLogger.Instance;
    }

    private long GetCollectionId(IDocumentTransaction? transaction = null)
    {
        if (_collectionId.HasValue)
            return _collectionId.Value;

        var connection = _database.GetConnection();
        var sql = "SELECT id FROM collections WHERE name = @Name LIMIT 1;";
        var id = connection.QuerySingleOrDefault<long?>(sql, new { Name = _collectionName }, transaction?.DbTransaction);

        if (!id.HasValue)
            throw new InvalidOperationException($"Collection '{_collectionName}' does not exist.");

        _collectionId = id.Value;
        return _collectionId.Value;
    }

    public void InsertOne(T document, IDocumentTransaction? transaction = null)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var collectionId = GetCollectionId(transaction);

        var id = GetDocumentId(document);
        // Check if ID is empty or default (ToString returns empty string for both)
        if (id == DocumentId.Empty || string.IsNullOrEmpty(id.ToString()))
        {
            id = DocumentId.NewId();
            SetDocumentId(document, id);
        }

        SetTimestamps(document, isNew: true);

        var json = DocumentSerializer.Serialize(document);
        var now = DateTime.UtcNow;

        var sql = @"
            INSERT INTO documents (collection_id, document_id, data, created_at, updated_at, version)
            VALUES (@CollectionId, @DocumentId, @Data, @CreatedAt, @UpdatedAt, 1);";

        try
        {
            var connection = _database.GetConnection();
            connection.Execute(sql, new
            {
                CollectionId = collectionId,
                DocumentId = id.ToString(),
                Data = json,
                CreatedAt = now.ToString("O"),
                UpdatedAt = now.ToString("O")
            }, transaction?.DbTransaction);

            _logger.LogDebug("Inserted document {Id} into {Collection}", id, _collectionName);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
        {
            throw new DuplicateKeyException(id, _collectionName);
        }
    }

    public void InsertMany(IEnumerable<T> documents, IDocumentTransaction? transaction = null)
    {
        if (documents == null)
            throw new ArgumentNullException(nameof(documents));

        var documentList = documents.ToList();
        if (documentList.Count == 0)
            return;

        foreach (var document in documentList)
        {
            InsertOne(document, transaction);
        }

        _logger.LogDebug("Inserted {Count} documents into {Collection}", documentList.Count, _collectionName);
    }

    public T? FindById(DocumentId id, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);

        var sql = "SELECT data FROM documents WHERE collection_id = @CollectionId AND document_id = @DocumentId LIMIT 1;";

        var connection = _database.GetConnection();
        var json = connection.QuerySingleOrDefault<string>(sql, new
        {
            CollectionId = collectionId,
            DocumentId = id.ToString()
        }, transaction?.DbTransaction);

        if (string.IsNullOrEmpty(json))
            return null;

        return DocumentSerializer.Deserialize<T>(json);
    }

    public T? FindOne(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"SELECT data FROM documents WHERE collection_id = @CollectionId AND {whereClause} LIMIT 1;";

        var connection = _database.GetConnection();
        var dynamicParams = CreateDynamicParameters(parameters);
        dynamicParams.Add("CollectionId", collectionId);
        var json = connection.QuerySingleOrDefault<string>(sql, dynamicParams, transaction?.DbTransaction);

        if (string.IsNullOrEmpty(json))
            return null;

        return DocumentSerializer.Deserialize<T>(json);
    }

    public List<T> Find(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"SELECT data FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        var connection = _database.GetConnection();
        var dynamicParams = CreateDynamicParameters(parameters);
        dynamicParams.Add("CollectionId", collectionId);
        var jsonResults = connection.Query<string>(sql, dynamicParams, transaction?.DbTransaction);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null)
                results.Add(doc);
        }

        return results;
    }

    public List<T> FindAll(IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);

        var sql = "SELECT data FROM documents WHERE collection_id = @CollectionId;";

        var connection = _database.GetConnection();
        var jsonResults = connection.Query<string>(sql, new { CollectionId = collectionId }, transaction: transaction?.DbTransaction);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null)
                results.Add(doc);
        }

        return results;
    }

    public List<T> Find(Expression<Func<T, bool>> filter, int skip, int limit, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"SELECT data FROM documents WHERE collection_id = @CollectionId AND {whereClause} LIMIT @Limit OFFSET @Skip;";

        var connection = _database.GetConnection();
        var dynamicParams = CreateDynamicParameters(parameters);
        dynamicParams.Add("CollectionId", collectionId);
        dynamicParams.Add("Limit", limit);
        dynamicParams.Add("Skip", skip);

        var jsonResults = connection.Query<string>(sql, dynamicParams, transaction?.DbTransaction);

        var results = new List<T>();
        foreach (var json in jsonResults)
        {
            var doc = DocumentSerializer.Deserialize<T>(json);
            if (doc != null)
                results.Add(doc);
        }

        return results;
    }

    public long Count(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"SELECT COUNT(*) FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        var connection = _database.GetConnection();
        var dynamicParams = CreateDynamicParameters(parameters);
        dynamicParams.Add("CollectionId", collectionId);
        return connection.ExecuteScalar<long>(sql, dynamicParams, transaction?.DbTransaction);
    }

    public long CountAll(IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);

        var sql = "SELECT COUNT(*) FROM documents WHERE collection_id = @CollectionId;";

        var connection = _database.GetConnection();
        return connection.ExecuteScalar<long>(sql, new { CollectionId = collectionId }, transaction: transaction?.DbTransaction);
    }

    public bool UpdateById(DocumentId id, T document, IDocumentTransaction? transaction = null)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var collectionId = GetCollectionId(transaction);

        SetDocumentId(document, id);
        SetTimestamps(document, isNew: false);

        var json = DocumentSerializer.Serialize(document);
        var now = DateTime.UtcNow;

        var sql = @"
            UPDATE documents
            SET data = @Data, updated_at = @UpdatedAt, version = version + 1
            WHERE collection_id = @CollectionId AND document_id = @DocumentId;";

        var connection = _database.GetConnection();
        var rowsAffected = connection.Execute(sql, new
        {
            CollectionId = collectionId,
            DocumentId = id.ToString(),
            Data = json,
            UpdatedAt = now.ToString("O")
        }, transaction?.DbTransaction);

        _logger.LogDebug("Updated document {Id} in {Collection}", id, _collectionName);

        return rowsAffected > 0;
    }

    public bool UpdateOne(Expression<Func<T, bool>> filter, T document, IDocumentTransaction? transaction = null)
    {
        var existing = FindOne(filter, transaction);
        if (existing == null)
            return false;

        var id = GetDocumentId(existing);
        return UpdateById(id, document, transaction);
    }

    public long UpdateMany(Expression<Func<T, bool>> filter, Action<T> updateAction, IDocumentTransaction? transaction = null)
    {
        var documents = Find(filter, transaction);
        long updatedCount = 0;

        foreach (var doc in documents)
        {
            updateAction(doc);
            var id = GetDocumentId(doc);
            if (UpdateById(id, doc, transaction))
                updatedCount++;
        }

        return updatedCount;
    }

    public bool DeleteById(DocumentId id, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);

        var sql = "DELETE FROM documents WHERE collection_id = @CollectionId AND document_id = @DocumentId;";

        var connection = _database.GetConnection();
        var rowsAffected = connection.Execute(sql, new
        {
            CollectionId = collectionId,
            DocumentId = id.ToString()
        }, transaction?.DbTransaction);

        _logger.LogDebug("Deleted document {Id} from {Collection}", id, _collectionName);

        return rowsAffected > 0;
    }

    public bool DeleteOne(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var doc = FindOne(filter, transaction);
        if (doc == null)
            return false;

        var id = GetDocumentId(doc);
        return DeleteById(id, transaction);
    }

    public long DeleteMany(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var collectionId = GetCollectionId(transaction);
        var (whereClause, parameters) = QueryTranslator.Translate(filter);

        var sql = $"DELETE FROM documents WHERE collection_id = @CollectionId AND {whereClause};";

        var connection = _database.GetConnection();
        var dynamicParams = CreateDynamicParameters(parameters);
        dynamicParams.Add("CollectionId", collectionId);
        var rowsAffected = connection.Execute(sql, dynamicParams, transaction?.DbTransaction);

        _logger.LogDebug("Deleted {Count} documents from {Collection}", rowsAffected, _collectionName);

        return rowsAffected;
    }

    public bool Any(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null)
    {
        var count = Count(filter, transaction);
        return count > 0;
    }

    public void CreateIndex<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false)
    {
        var collectionId = GetCollectionId();
        var fieldName = GetFieldName(fieldExpression);
        var indexName = $"idx_{_collectionName}_{fieldName}";
        var uniqueKeyword = unique ? "UNIQUE" : "";

        var connection = _database.GetConnection();

        // Insert into indexes metadata table
        var insertIndexSql = @"
            INSERT OR IGNORE INTO indexes (collection_id, name, fields, unique_index, sparse)
            VALUES (@CollectionId, @Name, @Fields, @Unique, 0);";

        connection.Execute(insertIndexSql, new
        {
            CollectionId = collectionId,
            Name = indexName,
            Fields = $"[{fieldName}]",
            Unique = unique ? 1 : 0
        });

        // Create actual SQLite index on documents table
        var createIndexSql = $@"
            CREATE {uniqueKeyword} INDEX IF NOT EXISTS {indexName}
            ON documents (json_extract(data, '$.{fieldName}'))
            WHERE collection_id = {collectionId};";

        connection.Execute(createIndexSql);

        _logger.LogInformation("Created index {IndexName} on {Collection}", indexName, _collectionName);
    }

    public void DropIndex<TField>(Expression<Func<T, TField>> fieldExpression)
    {
        var collectionId = GetCollectionId();
        var fieldName = GetFieldName(fieldExpression);
        var indexName = $"idx_{_collectionName}_{fieldName}";

        var connection = _database.GetConnection();

        // Drop the actual SQLite index
        var dropIndexSql = $"DROP INDEX IF EXISTS {indexName};";
        connection.Execute(dropIndexSql);

        // Delete from indexes metadata table
        var deleteIndexSql = @"
            DELETE FROM indexes
            WHERE collection_id = @CollectionId AND name = @Name;";

        connection.Execute(deleteIndexSql, new
        {
            CollectionId = collectionId,
            Name = indexName
        });

        _logger.LogInformation("Dropped index {IndexName} on {Collection}", indexName, _collectionName);
    }

    private static DocumentId GetDocumentId(T document)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(DocumentId))
        {
            return (DocumentId)(idProperty.GetValue(document) ?? DocumentId.Empty);
        }

        // Check for _id field
        var idField = typeof(T).GetField("_id");
        if (idField != null && idField.FieldType == typeof(DocumentId))
        {
            return (DocumentId)(idField.GetValue(document) ?? DocumentId.Empty);
        }

        return DocumentId.Empty;
    }

    private static void SetDocumentId(T document, DocumentId id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(DocumentId) && idProperty.CanWrite)
        {
            idProperty.SetValue(document, id);
            return;
        }

        var idField = typeof(T).GetField("_id");
        if (idField != null && idField.FieldType == typeof(DocumentId))
        {
            idField.SetValue(document, id);
        }
    }

    private static void SetTimestamps(T document, bool isNew)
    {
        var now = DateTime.UtcNow;

        if (isNew)
        {
            var createdAtProperty = typeof(T).GetProperty("CreatedAt");
            if (createdAtProperty != null && createdAtProperty.PropertyType == typeof(DateTime) && createdAtProperty.CanWrite)
            {
                createdAtProperty.SetValue(document, now);
            }
        }

        var updatedAtProperty = typeof(T).GetProperty("UpdatedAt");
        if (updatedAtProperty != null && updatedAtProperty.PropertyType == typeof(DateTime) && updatedAtProperty.CanWrite)
        {
            updatedAtProperty.SetValue(document, now);
        }
    }

    private static string GetFieldName<TField>(Expression<Func<T, TField>> fieldExpression)
    {
        if (fieldExpression.Body is MemberExpression member)
        {
            return ToCamelCase(member.Member.Name);
        }

        throw new InvalidQueryException("Invalid field expression", fieldExpression.ToString());
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    private static DynamicParameters CreateDynamicParameters(IReadOnlyList<object?> parameters)
    {
        var dynamicParams = new DynamicParameters();
        for (int i = 0; i < parameters.Count; i++)
        {
            dynamicParams.Add($"p{i}", parameters[i]);
        }
        return dynamicParams;
    }
}
