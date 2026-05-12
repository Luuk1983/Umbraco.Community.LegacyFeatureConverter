namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Tracks the conversion status of a single content node during a conversion operation.
/// </summary>
public class ContentConversionInfo
{
    /// <summary>
    /// Gets or sets the integer ID of the content node.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the unique key of the content node.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// Gets or sets the display name of the content node.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the content node was converted successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content node was skipped
    /// (e.g., no properties to convert).
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

    /// <summary>
    /// Gets or sets the number of properties that were converted on this content node.
    /// </summary>
    public int PropertiesConverted { get; set; }
}
