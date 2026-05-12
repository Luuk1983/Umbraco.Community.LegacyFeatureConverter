namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Database entity representing a single log entry within a conversion run.
/// Provides detailed, structured logging for audit and debugging purposes.
/// </summary>
public class ConversionLogEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this log entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the conversion this log entry belongs to.
    /// </summary>
    public Guid ConversionHistoryId { get; set; }

    /// <summary>
    /// Gets or sets when this log entry was created (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the log level (e.g., "Information", "Warning", "Error").
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of item being logged (e.g., "DocumentType", "DataType", "Content", "Property").
    /// </summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the item being processed, if applicable.
    /// </summary>
    public string? ItemName { get; set; }

    /// <summary>
    /// Gets or sets the key/ID of the item being processed, if applicable.
    /// </summary>
    public string? ItemKey { get; set; }

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional details as a JSON string, if applicable.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the exception stack trace, if this is an error log entry.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the parent conversion history record.
    /// </summary>
    public ConversionHistory? ConversionHistory { get; set; }
}
