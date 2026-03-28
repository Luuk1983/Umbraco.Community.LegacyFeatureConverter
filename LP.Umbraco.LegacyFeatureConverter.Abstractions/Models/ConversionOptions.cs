namespace LP.Umbraco.LegacyFeatureConverter.Models;

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
    /// Gets or sets the key of the Umbraco backoffice user performing the conversion.
    /// </summary>
    public Guid PerformingUserKey { get; set; }
}
