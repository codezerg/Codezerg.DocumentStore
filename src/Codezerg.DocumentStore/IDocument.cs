using System;

namespace Codezerg.DocumentStore;

/// <summary>
/// Base interface for all documents stored in a document collection.
/// All document types must implement this interface to ensure proper ID management and tracking.
/// </summary>
public interface IDocument
{
    /// <summary>
    /// Gets or sets the unique identifier for this document.
    /// </summary>
    DocumentId Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this document was created.
    /// This property is automatically set when the document is first inserted.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this document was last updated.
    /// This property is automatically updated on every save operation.
    /// </summary>
    DateTime UpdatedAt { get; set; }
}
