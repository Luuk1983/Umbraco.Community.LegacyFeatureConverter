namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Represents real-time progress information for a running conversion.
/// Used for SignalR broadcasting to the backoffice UI.
/// </summary>
public class ConversionProgress
{
    /// <summary>
    /// Gets or sets the unique identifier of the conversion this progress belongs to.
    /// </summary>
    public Guid ConversionId { get; set; }

    /// <summary>
    /// Gets or sets the current phase of the conversion (e.g., "Scanning document types",
    /// "Creating data types", "Updating document types", "Converting content").
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the item currently being processed.
    /// </summary>
    public string CurrentItem { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of items processed so far in the current phase.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of items to process in the current phase.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets the percentage of completion for the current phase (0-100).
    /// Returns 0 when <see cref="TotalCount"/> is zero to avoid division by zero.
    /// </summary>
    public int PercentComplete => TotalCount > 0
        ? (int)Math.Round((double)ProcessedCount / TotalCount * 100)
        : 0;

    /// <summary>
    /// Gets or sets the overall status of the conversion.
    /// </summary>
    public ConversionStatus Status { get; set; } = ConversionStatus.Running;
}
