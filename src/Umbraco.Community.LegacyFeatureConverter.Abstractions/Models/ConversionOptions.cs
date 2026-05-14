namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Options for configuring a conversion run.
/// </summary>
public class ConversionOptions
{
    /// <summary>
    /// Gets or sets the type of converter to use (e.g., "Nested Content to Block List").
    /// </summary>
    public string ConverterType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keys of document types to convert.
    /// If null or empty, all affected document types will be converted.
    /// </summary>
    public Guid[]? SelectedDocumentTypeKeys { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a test run (dry run).
    /// When true, no changes will be saved to the database.
    /// </summary>
    public bool IsTestRun { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversion should stop
    /// when an individual item fails, or continue with the next item.
    /// </summary>
    public bool StopOnError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a test run should be executed
    /// before the actual conversion. If the test run fails, the actual conversion
    /// will not proceed.
    /// </summary>
    public bool RunTestFirst { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether converted content should also be published.
    /// When false (default), content is saved as a draft only.
    /// When true, content is saved and published after conversion.
    /// Has no effect when <see cref="IsTestRun"/> is true.
    /// </summary>
    public bool PublishAfterConversion { get; set; }

    /// <summary>
    /// Gets or sets the key of the Umbraco backoffice user performing the conversion.
    /// </summary>
    public Guid PerformingUserKey { get; set; }

    /// <summary>
    /// Gets or sets the approach used to discover which document types and content to process.
    /// Defaults to <see cref="ConversionApproach.Fast"/>.
    /// </summary>
    public ConversionApproach Approach { get; set; } = ConversionApproach.Fast;

    /// <summary>
    /// Gets or sets the pre-computed conversion plan from the wizard.
    /// When provided, the background task uses this plan directly instead of re-scanning,
    /// avoiding duplicate work and ensuring the migration matches the preview exactly.
    /// </summary>
    public ConversionPlan? Plan { get; set; }

    /// <summary>
    /// Gets or sets the keys of macros to convert (only used by macro converters).
    /// If null or empty, all macros registered in Umbraco will be converted.
    /// </summary>
    public Guid[]? SelectedMacroKeys { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stub partial views should be generated for
    /// each converted macro at <c>/Views/Partials/richtext/Components/{MacroName}.cshtml</c>,
    /// containing migration instructions and the original macro view as a comment.
    /// Default is true. Only used by macro converters. Honors <see cref="IsTestRun"/>:
    /// no files are written during a dry run.
    /// </summary>
    public bool GenerateStubPartialViews { get; set; } = true;
}
