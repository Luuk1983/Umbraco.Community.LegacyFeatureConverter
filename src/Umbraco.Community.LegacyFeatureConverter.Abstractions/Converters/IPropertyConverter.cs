using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Defines the contract for property editor converters.
/// Implementations convert legacy Umbraco property editors to their modern equivalents.
///
/// Package consumers can implement this interface and register their converter
/// in the DI container to add custom conversions. The converter will be automatically
/// discovered by the <see cref="Services.IConverterService"/>.
///
/// Shared metadata (<see cref="ILegacyFeatureConverter.ConverterName"/>,
/// <see cref="ILegacyFeatureConverter.Description"/>, <see cref="ILegacyFeatureConverter.ShortName"/>,
/// <see cref="ILegacyFeatureConverter.Icon"/>, <see cref="ILegacyFeatureConverter.Category"/>) is
/// inherited from <see cref="ILegacyFeatureConverter"/>.
/// </summary>
public interface IPropertyConverter : ILegacyFeatureConverter
{
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

    /// <summary>
    /// Default <see cref="ILegacyFeatureConverter.GetAffectedUnitCountAsync"/> implementation:
    /// property converters report affected document types as their "unit".
    /// </summary>
    Task<int> ILegacyFeatureConverter.GetAffectedUnitCountAsync(CancellationToken cancellationToken)
        => GetAffectedDocumentTypesCountAsync(null, cancellationToken);
}
