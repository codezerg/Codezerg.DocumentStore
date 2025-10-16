using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Codezerg.DocumentStore;

/// <summary>
/// Represents a collection of documents with type-safe operations.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentCollection<T> where T : class
{
    /// <summary>
    /// Gets the collection name.
    /// </summary>
    string CollectionName { get; }

    /// <summary>
    /// Inserts a single document into the collection.
    /// </summary>
    /// <param name="document">The document to insert.</param>
    /// <param name="transaction">Optional transaction.</param>
    void InsertOne(T document, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Inserts multiple documents into the collection.
    /// </summary>
    /// <param name="documents">The documents to insert.</param>
    /// <param name="transaction">Optional transaction.</param>
    void InsertMany(IEnumerable<T> documents, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Finds a document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The document if found, null otherwise.</returns>
    T? FindById(DocumentId id, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Finds a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The first matching document, or null if not found.</returns>
    T? FindOne(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Finds all documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>A list of matching documents.</returns>
    List<T> Find(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Finds all documents in the collection.
    /// </summary>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>A list of all documents.</returns>
    List<T> FindAll(IDocumentTransaction? transaction = null);

    /// <summary>
    /// Finds documents with pagination support.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="skip">Number of documents to skip.</param>
    /// <param name="limit">Maximum number of documents to return.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>A list of matching documents.</returns>
    List<T> Find(Expression<Func<T, bool>> filter, int skip, int limit, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Counts documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The count of matching documents.</returns>
    long Count(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Counts all documents in the collection.
    /// </summary>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The total count of documents.</returns>
    long CountAll(IDocumentTransaction? transaction = null);

    /// <summary>
    /// Updates a single document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="document">The updated document.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>True if the document was updated, false if not found.</returns>
    bool UpdateById(DocumentId id, T document, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Updates a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="document">The updated document.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>True if a document was updated, false otherwise.</returns>
    bool UpdateOne(Expression<Func<T, bool>> filter, T document, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Updates multiple documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="updateAction">An action to update each matching document.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The number of documents updated.</returns>
    long UpdateMany(Expression<Func<T, bool>> filter, Action<T> updateAction, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Deletes a single document by its ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>True if the document was deleted, false if not found.</returns>
    bool DeleteById(DocumentId id, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Deletes a single document matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>True if a document was deleted, false otherwise.</returns>
    bool DeleteOne(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Deletes all documents matching the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>The number of documents deleted.</returns>
    long DeleteMany(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Checks if any document matches the filter.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>True if any document matches, false otherwise.</returns>
    bool Any(Expression<Func<T, bool>> filter, IDocumentTransaction? transaction = null);

    /// <summary>
    /// Creates an index on the specified field.
    /// </summary>
    /// <param name="fieldExpression">The field expression.</param>
    /// <param name="unique">Whether the index should be unique.</param>
    void CreateIndex<TField>(Expression<Func<T, TField>> fieldExpression, bool unique = false);

    /// <summary>
    /// Drops an index on the specified field.
    /// </summary>
    /// <param name="fieldExpression">The field expression.</param>
    void DropIndex<TField>(Expression<Func<T, TField>> fieldExpression);
}
