namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Represents the status of a conversion operation.
/// </summary>
public enum ConversionStatus
{
    /// <summary>
    /// Conversion is queued and waiting to be processed.
    /// </summary>
    Queued,

    /// <summary>
    /// Conversion is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Conversion completed successfully with no errors.
    /// </summary>
    Completed,

    /// <summary>
    /// Conversion completed but some items encountered errors.
    /// </summary>
    CompletedWithErrors,

    /// <summary>
    /// Conversion failed critically and could not complete.
    /// </summary>
    Failed,

    /// <summary>
    /// Conversion was cancelled by the user.
    /// </summary>
    Cancelled
}
