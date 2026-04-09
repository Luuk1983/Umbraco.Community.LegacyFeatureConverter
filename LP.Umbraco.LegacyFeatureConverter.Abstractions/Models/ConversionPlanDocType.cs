namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Represents a single document type's role in a pre-computed conversion plan.
/// </summary>
public class ConversionPlanDocType
{
    /// <summary>Gets or sets the document type key.</summary>
    public Guid Key { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the alias.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Gets or sets the backoffice icon (e.g. "icon-document").</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the number of content nodes that will be processed.
    /// For DocumentType approach this is the total count of all nodes.
    /// For Content approach this is the count of nodes with unconverted values.
    /// </summary>
    public int ContentNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of properties (on the document type definition) that need conversion.
    /// </summary>
    public int PropertyCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the document type schema (property editor alias)
    /// needs to be updated. False when the document type was already updated by uSync.
    /// </summary>
    public bool NeedsSchemaUpdate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether content nodes of this document type
    /// contain values that need to be converted.
    /// </summary>
    public bool NeedsContentUpdate { get; set; }
}
