using System;

namespace Codezerg.DocumentStore.Exceptions;

/// <summary>
/// Exception thrown when a document is not found in the collection.
/// </summary>
public class DocumentNotFoundException : Exception
{
    /// <summary>
    /// Gets the ID of the document that was not found.
    /// </summary>
    public DocumentId? DocumentId { get; }

    /// <summary>
    /// Gets the name of the collection.
    /// </summary>
    public string? CollectionName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentNotFoundException"/> class.
    /// </summary>
    public DocumentNotFoundException()
        : base("Document not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentNotFoundException"/> class with a message.
    /// </summary>
    public DocumentNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentNotFoundException"/> class with a message and inner exception.
    /// </summary>
    public DocumentNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentNotFoundException"/> class with document details.
    /// </summary>
    public DocumentNotFoundException(DocumentId documentId, string collectionName)
        : base($"Document with ID '{documentId}' not found in collection '{collectionName}'.")
    {
        DocumentId = documentId;
        CollectionName = collectionName;
    }
}
