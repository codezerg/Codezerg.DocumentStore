using System;

namespace Codezerg.DocumentStore.Exceptions;

/// <summary>
/// Exception thrown when a query is invalid or cannot be translated.
/// </summary>
public class InvalidQueryException : Exception
{
    /// <summary>
    /// Gets the query expression that caused the error.
    /// </summary>
    public string? QueryExpression { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidQueryException"/> class.
    /// </summary>
    public InvalidQueryException()
        : base("Invalid query.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidQueryException"/> class with a message.
    /// </summary>
    public InvalidQueryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidQueryException"/> class with a message and inner exception.
    /// </summary>
    public InvalidQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidQueryException"/> class with query details.
    /// </summary>
    public InvalidQueryException(string message, string queryExpression)
        : base($"{message} Query: {queryExpression}")
    {
        QueryExpression = queryExpression;
    }
}
