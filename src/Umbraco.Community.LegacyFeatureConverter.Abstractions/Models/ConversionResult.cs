namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Represents the complete result of a conversion operation, including status,
/// timing information, and detailed results for each processed item.
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Gets or sets the unique identifier for this conversion run.
    /// </summary>
    public Guid ConversionId { get; set; }

    /// <summary>
    /// Gets or sets the name of the converter that was used (e.g., "Nested Content to Block List").
    /// </summary>
    public string ConverterType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this was a test run (dry run).
    /// </summary>
    public bool IsTestRun { get; set; }

    /// <summary>
    /// Gets or sets the overall status of the conversion.
    /// </summary>
    public ConversionStatus Status { get; set; } = ConversionStatus.Running;

    /// <summary>
    /// Gets or sets the list of document types that were scanned and processed.
    /// </summary>
    public List<DocumentTypeConversionInfo> DocumentTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of data types that were created or updated.
    /// </summary>
    public List<DataTypeConversionInfo> DataTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of content nodes that were converted.
    /// </summary>
    public List<ContentConversionInfo> ContentNodes { get; set; } = new();

    /// <summary>
    /// Gets the total count of all items processed across all categories.
    /// </summary>
    public int TotalItems => DocumentTypes.Count + DataTypes.Count + ContentNodes.Count;

    /// <summary>
    /// Gets the count of items that were processed successfully.
    /// </summary>
    public int SuccessCount =>
        DocumentTypes.Count(x => x.Success) +
        DataTypes.Count(x => x.Success) +
        ContentNodes.Count(x => x.Success);

    /// <summary>
    /// Gets the count of items that failed (not successful and not skipped).
    /// </summary>
    public int FailureCount =>
        DocumentTypes.Count(x => !x.Success && !x.Skipped) +
        DataTypes.Count(x => !x.Success && !x.Skipped) +
        ContentNodes.Count(x => !x.Success && !x.Skipped);

    /// <summary>
    /// Gets the count of items that were skipped.
    /// </summary>
    public int SkippedCount =>
        DocumentTypes.Count(x => x.Skipped) +
        DataTypes.Count(x => x.Skipped) +
        ContentNodes.Count(x => x.Skipped);

    /// <summary>
    /// Gets or sets when the conversion started (UTC).
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the conversion completed (UTC), or null if still running.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets the duration of the conversion, or null if not yet completed.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Gets or sets the error message if the conversion failed critically.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception stack trace if the conversion failed critically.
    /// </summary>
    public string? StackTrace { get; set; }
}
