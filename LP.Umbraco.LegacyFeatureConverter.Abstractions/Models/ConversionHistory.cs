namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Database entity representing a single conversion run.
/// Stores summary information and metadata about the conversion for audit purposes.
/// </summary>
public class ConversionHistory
{
    /// <summary>
    /// Gets or sets the unique identifier for this conversion run.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets when the conversion started (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the conversion completed (UTC), or null if still running.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the name of the converter that was used.
    /// </summary>
    public string ConverterType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this was a test run (dry run).
    /// </summary>
    public bool IsTestRun { get; set; }

    /// <summary>
    /// Gets or sets the overall status of the conversion (e.g., "Running", "Completed", "Failed").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of document types processed.
    /// </summary>
    public int TotalDocumentTypes { get; set; }

    /// <summary>
    /// Gets or sets the total number of data types processed.
    /// </summary>
    public int TotalDataTypes { get; set; }

    /// <summary>
    /// Gets or sets the total number of content nodes processed.
    /// </summary>
    public int TotalContentNodes { get; set; }

    /// <summary>
    /// Gets or sets the number of items that completed successfully.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of items that failed.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the number of items that were skipped.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized summary of the conversion result.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized array of selected document type keys,
    /// or null if all document types were selected.
    /// </summary>
    public string? SelectedDocumentTypes { get; set; }

    /// <summary>
    /// Gets or sets the key of the Umbraco backoffice user who performed the conversion.
    /// </summary>
    public Guid PerformingUserKey { get; set; }

    /// <summary>
    /// Gets or sets the collection of log entries associated with this conversion.
    /// </summary>
    public ICollection<ConversionLogEntry> LogEntries { get; set; } = new List<ConversionLogEntry>();
}
