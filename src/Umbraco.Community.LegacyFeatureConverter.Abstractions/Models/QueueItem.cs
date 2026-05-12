namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Database entity representing a queued conversion job.
/// Queue items survive application restarts because they are persisted to the database.
/// </summary>
public class QueueItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this queue item.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized <see cref="ConversionOptions"/> for this conversion.
    /// </summary>
    public string SerializedOptions { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the item was added to the queue (UTC).
    /// </summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// Gets or sets when the item started processing (UTC), or null if not yet started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the item finished processing (UTC), or null if not yet finished.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the current status of the queue item.
    /// </summary>
    public ConversionStatus Status { get; set; } = ConversionStatus.Queued;

    /// <summary>
    /// Gets or sets the ID of the conversion history record, once the conversion starts.
    /// </summary>
    public Guid? ConversionHistoryId { get; set; }
}
