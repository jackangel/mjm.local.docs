namespace Mjm.LocalDocs.Core.Models;

/// <summary>
/// Represents the result of a batch document upload operation.
/// </summary>
public sealed class BatchUploadResult
{
    /// <summary>
    /// Documents that were successfully added to the store.
    /// </summary>
    public required IReadOnlyList<Document> SuccessfulDocuments { get; init; }

    /// <summary>
    /// Documents that failed to be added.
    /// </summary>
    public required IReadOnlyList<FailedDocument> FailedDocuments { get; init; }

    /// <summary>
    /// Total number of documents in the batch.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Number of documents successfully added.
    /// </summary>
    public required int SuccessCount { get; init; }

    /// <summary>
    /// Number of documents that failed to be added.
    /// </summary>
    public required int FailureCount { get; init; }

    /// <summary>
    /// Represents a document that failed to upload.
    /// </summary>
    public sealed class FailedDocument
    {
        /// <summary>
        /// The original file name of the document that failed.
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Error message describing why the upload failed.
        /// </summary>
        public required string ErrorMessage { get; init; }

        /// <summary>
        /// The exception that caused the failure, if available.
        /// </summary>
        public Exception? Exception { get; init; }
    }
}
