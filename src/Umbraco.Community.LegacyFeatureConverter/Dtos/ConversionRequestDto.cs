using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Dtos;

/// <summary>
/// Request model for queuing a new conversion.
/// </summary>
public class ConversionRequestDto
{
    /// <summary>
    /// Gets or sets the name of the converter to use (e.g., "Nested Content to Block List").
    /// </summary>
    public string ConverterType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keys of document types to convert.
    /// If null or empty, all affected document types will be converted.
    /// </summary>
    public Guid[]? SelectedDocumentTypeKeys { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a test run (dry run).
    /// </summary>
    public bool IsTestRun { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversion should stop on error.
    /// </summary>
    public bool StopOnError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test run should be executed first.
    /// </summary>
    public bool RunTestFirst { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether converted content should also be published.
    /// When false (default), content is saved as a draft only.
    /// </summary>
    public bool PublishAfterConversion { get; set; }

    /// <summary>
    /// Gets or sets the approach used to discover affected document types and content.
    /// </summary>
    public ConversionApproach Approach { get; set; } = ConversionApproach.Fast;

    /// <summary>
    /// Gets or sets the pre-computed conversion plan from the wizard impact step.
    /// When provided, the background task uses this directly instead of re-scanning.
    /// </summary>
    public ConversionPlan? Plan { get; set; }

    /// <summary>
    /// Gets or sets the keys of macros to convert (only used by macro converters).
    /// If null or empty, all macros registered in Umbraco will be converted.
    /// </summary>
    public Guid[]? SelectedMacroKeys { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stub partial views should be generated for
    /// each converted macro. Defaults to true on the model so the wizard's checkbox starts checked.
    /// </summary>
    public bool GenerateStubPartialViews { get; set; } = true;
}
