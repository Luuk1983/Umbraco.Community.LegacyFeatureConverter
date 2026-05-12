using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Defines the contract for property editor converters.
/// Implementations convert legacy Umbraco property editors to their modern equivalents.
///
/// Package consumers can implement this interface and register their converter
/// in the DI container to add custom conversions. The converter will be automatically
/// discovered by the <see cref="Services.IConverterService"/>.
/// </summary>
public interface IPropertyConverter
{
    /// <summary>
    /// Gets the human-readable name of this converter (e.g., "Nested Content to Block List").
    /// </summary>
    string ConverterName { get; }

    /// <summary>
    /// Gets the property editor aliases this converter can convert FROM
    /// (e.g., <c>["Umbraco.NestedContent"]</c>).
    /// </summary>
    string[] SourcePropertyEditorAliases { get; }

    /// <summary>
    /// Gets the property editor alias this converter converts TO
    /// (e.g., <c>"Umbraco.BlockList"</c>).
    /// </summary>
    string TargetPropertyEditorAlias { get; }

    /// <summary>
    /// Gets a brief description of what this converter does, displayed in the backoffice UI.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a short display name for the converter card UI (e.g., "Nested Content", "Media Picker").
    /// Defaults to <see cref="ConverterName"/> if not overridden.
    /// </summary>
    string ShortName => ConverterName;

    /// <summary>
    /// Gets the Umbraco backoffice icon for this converter (e.g., "icon-axis-rotation").
    /// Used in the converter picker card UI.
    /// </summary>
    string Icon => "icon-axis-rotation";

    /// <summary>
    /// Gets the category label for this converter (e.g., "Property editor").
    /// Used to label converters in the picker card UI.
    /// </summary>
    string Category => "Property editor";

    /// <summary>
    /// Executes the conversion process with the specified options.
    /// </summary>
    /// <param name="options">The conversion options including document type selection and test run flag.</param>
    /// <param name="progress">Optional progress reporter for real-time UI updates via SignalR.</param>
    /// <param name="cancellationToken">Token to support graceful cancellation.</param>
    /// <returns>The conversion result containing detailed status for each processed item.</returns>
    Task<ConversionResult> ExecuteConversionAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a count of document types that would be affected by this converter.
    /// Uses the DocumentType approach (schema scan) for speed.
    /// </summary>
    /// <param name="selectedDocumentTypeKeys">Optional array of document type keys to filter. If null, counts all affected document types.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The count of affected document types.</returns>
    Task<int> GetAffectedDocumentTypesCountAsync(
        Guid[]? selectedDocumentTypeKeys = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a detailed conversion plan showing exactly which document types and content
    /// nodes will be affected. The plan is stored with the queue item so the background task
    /// can execute the migration without re-scanning.
    /// </summary>
    /// <param name="approach">How to discover affected document types and content.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A plan describing every document type and content node to be changed.</returns>
    Task<ConversionPlan> ComputePlanAsync(
        ConversionApproach approach,
        CancellationToken cancellationToken = default);
}
