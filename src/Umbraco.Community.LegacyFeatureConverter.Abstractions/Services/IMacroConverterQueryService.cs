using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Services;

/// <summary>
/// Scans Umbraco content for macro usage. Shared between the macro converter (which uses the
/// scan results to drive its work) and the API controller (which uses them to populate the
/// wizard's macro-selection step), so the same scan logic isn't implemented twice.
///
/// The scan covers:
/// <list type="bullet">
///   <item>Direct Rich Text Editor properties on every content node.</item>
///   <item>Rich Text Editor properties nested inside BlockList / BlockGrid elements (recursive).</item>
/// </list>
/// Legacy Grid <c>macro</c> cells are out of scope for v1.
/// </summary>
public interface IMacroConverterQueryService
{
    /// <summary>
    /// Performs a full scan of content for macro usage and returns aggregated results.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The aggregated macro usage information for every distinct alias found.</returns>
    Task<MacroScanResult> ScanForMacroUsageAsync(CancellationToken cancellationToken = default);
}
