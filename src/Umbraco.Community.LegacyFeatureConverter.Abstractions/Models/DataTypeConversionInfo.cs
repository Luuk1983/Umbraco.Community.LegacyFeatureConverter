namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Tracks the conversion status of a single data type during a conversion operation.
/// </summary>
public class DataTypeConversionInfo
{
    /// <summary>
    /// Gets or sets the integer ID of the data type.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the data type.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique key of the data type.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data type was created/updated successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data type was skipped
    /// (e.g., target data type already existed).
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// Gets or sets an informational message about the conversion.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the error message if the conversion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
