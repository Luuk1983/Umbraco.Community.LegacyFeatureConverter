namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Information about a document type for display in the conversion wizard.
/// Used when the editor selects which document types to include in a conversion.
/// </summary>
public class DocumentTypeInfo
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
    /// Gets or sets the icon of the document type (e.g., "icon-document").
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the number of properties using the source property editor on this document type.
    /// </summary>
    public int PropertiesCount { get; set; }
}
