using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Services;

/// <summary>
/// Service for discovering and managing property converters.
/// Provides access to all registered converters and their metadata.
/// Converters are automatically discovered via dependency injection.
/// </summary>
public interface IConverterService
{
    /// <summary>
    /// Gets all registered property converters.
    /// </summary>
    /// <returns>All property converters registered in the DI container.</returns>
    IEnumerable<IPropertyConverter> GetAllConverters();

    /// <summary>
    /// Gets a specific converter by its name.
    /// </summary>
    /// <param name="converterName">The name of the converter to find (case-insensitive).</param>
    /// <returns>The matching converter, or null if not found.</returns>
    IPropertyConverter? GetConverterByName(string converterName);

    /// <summary>
    /// Gets metadata for all registered converters, suitable for display in the backoffice UI.
    /// Includes the count of affected document types for each converter.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>Metadata for all registered converters.</returns>
    Task<IEnumerable<ConverterMetadata>> GetConverterMetadataAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document types that would be affected by a specific converter.
    /// </summary>
    /// <param name="converterName">The name of the converter.</param>
    /// <param name="selectedKeys">Optional array of document type keys to filter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>Information about affected document types, ordered by name.</returns>
    Task<IEnumerable<DocumentTypeInfo>> GetAffectedDocumentTypesAsync(
        string converterName,
        Guid[]? selectedKeys = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a detailed conversion plan for a given converter and approach.
    /// </summary>
    /// <param name="converterName">The name of the converter.</param>
    /// <param name="approach">How to discover affected document types and content.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A plan describing every document type and content node to be changed.</returns>
    Task<ConversionPlan> ComputePlanAsync(
        string converterName,
        ConversionApproach approach,
        CancellationToken cancellationToken = default);
}
