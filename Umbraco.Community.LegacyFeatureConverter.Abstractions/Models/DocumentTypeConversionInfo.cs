namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Tracks the conversion status of a single document type during a conversion operation.
/// </summary>
public class DocumentTypeConversionInfo
{
    /// <summary>
    /// Gets or sets the unique key of the document type.
    /// </summary>
    public Guid Key { get; set; }

    /// <summary>
    /// Gets or sets the display name of the document type.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the alias of the document type.
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the document type was converted successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the document type was skipped
    /// (e.g., no properties to update).
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
    /// Gets or sets the number of properties that were updated on this document type.
    /// </summary>
    public int PropertiesUpdated { get; set; }
}
