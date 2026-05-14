using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Defines the contract for macro converters.
/// Implementations convert legacy Umbraco macro usages to a modern block-based equivalent.
///
/// Package consumers can implement this interface and register their converter in the DI
/// container to add custom macro conversion strategies. Converters are auto-discovered by
/// the <see cref="Services.IConverterService"/> alongside <see cref="IPropertyConverter"/>.
///
/// Shared metadata (<see cref="ILegacyFeatureConverter.ConverterName"/>,
/// <see cref="ILegacyFeatureConverter.Description"/>, <see cref="ILegacyFeatureConverter.ShortName"/>,
/// <see cref="ILegacyFeatureConverter.Icon"/>, <see cref="ILegacyFeatureConverter.Category"/>) is
/// inherited from <see cref="ILegacyFeatureConverter"/>.
/// </summary>
public interface IMacroConverter : ILegacyFeatureConverter
{
    /// <summary>
    /// Gets a short alias identifying the target shape this converter emits
    /// (e.g., <c>"richTextBlock"</c> for macros replaced with inline <c>&lt;umb-rte-block&gt;</c>,
    /// <c>"blockList"</c> for whole-page macros promoted to a Block List property).
    /// Used to disambiguate when multiple macro converters target different shapes.
    /// </summary>
    string TargetShapeAlias { get; }

    /// <summary>
    /// Executes the macro conversion process with the specified options.
    /// </summary>
    /// <param name="options">The conversion options including macro selection and test run flag.</param>
    /// <param name="progress">Optional progress reporter for real-time UI updates via SignalR.</param>
    /// <param name="cancellationToken">Token to support graceful cancellation.</param>
    /// <returns>The conversion result containing detailed status for each processed item.</returns>
    Task<ConversionResult> ExecuteConversionAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a detailed conversion plan showing exactly which macros and content nodes
    /// will be affected. The plan is stored with the queue item so the background task
    /// can execute the migration without re-scanning.
    /// </summary>
    /// <param name="approach">How to discover affected content (DocumentType or Thorough).</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A plan describing every macro and content node to be changed.</returns>
    Task<ConversionPlan> ComputePlanAsync(
        ConversionApproach approach,
        CancellationToken cancellationToken = default);
}
