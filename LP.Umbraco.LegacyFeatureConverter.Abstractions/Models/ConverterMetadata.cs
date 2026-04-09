namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Metadata about a property converter for display in the backoffice UI.
/// </summary>
public class ConverterMetadata
{
    /// <summary>
    /// Gets or sets the human-readable name of the converter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief description of what the converter does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a short display name for the converter card UI.
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Umbraco backoffice icon for this converter.
    /// </summary>
    public string Icon { get; set; } = "icon-axis-rotation";

    /// <summary>
    /// Gets or sets the category label (e.g., "Property editor").
    /// </summary>
    public string Category { get; set; } = "Property editor";

    /// <summary>
    /// Gets or sets the property editor aliases this converter can convert from.
    /// </summary>
    public string[] SourceAliases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the property editor alias this converter converts to.
    /// </summary>
    public string TargetAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the count of document types that would be affected by this converter.
    /// </summary>
    public int AffectedDocumentTypesCount { get; set; }
}
