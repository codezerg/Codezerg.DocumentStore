using System;

namespace Codezerg.DocumentStore.Exceptions;

/// <summary>
/// Exception thrown when attempting to insert a document with a duplicate key.
/// </summary>
public class DuplicateKeyException : Exception
{
    /// <summary>
    /// Gets the key value that caused the duplicate.
    /// </summary>
    public object? KeyValue { get; }

    /// <summary>
    /// Gets the name of the collection.
    /// </summary>
    public string? CollectionName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class.
    /// </summary>
    public DuplicateKeyException()
        : base("Duplicate key error.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class with a message.
    /// </summary>
    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class with a message and inner exception.
    /// </summary>
    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateKeyException"/> class with key details.
    /// </summary>
    public DuplicateKeyException(object keyValue, string collectionName)
        : base($"Duplicate key '{keyValue}' in collection '{collectionName}'.")
    {
        KeyValue = keyValue;
        CollectionName = collectionName;
    }
}
