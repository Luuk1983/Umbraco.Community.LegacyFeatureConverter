namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// A pre-computed conversion plan that describes exactly what will be changed.
/// Computed upfront in the wizard and stored with the queue item to avoid
/// re-scanning during the actual conversion.
/// </summary>
public class ConversionPlan
{
    /// <summary>Gets or sets the approach used to compute this plan.</summary>
    public ConversionApproach Approach { get; set; }

    /// <summary>Gets or sets the document types included in this plan.</summary>
    public List<ConversionPlanDocType> DocumentTypes { get; set; } = new();

    /// <summary>Gets or sets the total number of content nodes across all document types.</summary>
    public int TotalContentNodes { get; set; }

    /// <summary>Gets or sets the total number of properties across all document types.</summary>
    public int TotalPropertyCount { get; set; }

    /// <summary>
    /// Gets or sets the macros included in this plan.
    /// Only populated by macro converters; empty for property converters.
    /// </summary>
    public List<ConversionPlanMacro> Macros { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of macro usages across all RTE properties and content nodes.
    /// Only populated by macro converters; zero for property converters.
    /// </summary>
    public int TotalMacroUsages { get; set; }

    /// <summary>Gets or sets when this plan was computed.</summary>
    public DateTime ComputedAt { get; set; }
}
