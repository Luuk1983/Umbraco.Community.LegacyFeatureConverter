namespace LP.Umbraco.LegacyFeatureConverter.Dtos;

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
}
