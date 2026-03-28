using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Hubs;

/// <summary>
/// Defines the client-side methods that the server can invoke via SignalR.
/// These method names must match the JavaScript client-side event handlers
/// in the backoffice UI.
/// </summary>
public interface IConversionHubClient
{
    /// <summary>
    /// Called when conversion progress is updated (phase change, item processed).
    /// </summary>
    /// <param name="progress">The current progress information.</param>
    Task ReceiveProgress(ConversionProgress progress);

    /// <summary>
    /// Called when a conversion completes (successfully, with errors, or failed).
    /// </summary>
    /// <param name="queueItemId">The ID of the queue item that completed.</param>
    /// <param name="status">The final conversion status.</param>
    /// <param name="conversionHistoryId">The ID of the conversion history record, if available.</param>
    Task ConversionCompleted(Guid queueItemId, ConversionStatus status, Guid? conversionHistoryId);
}
