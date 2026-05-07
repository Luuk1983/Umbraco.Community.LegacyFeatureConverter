using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Services;

/// <summary>
/// Factory for creating progress reporters that broadcast conversion progress via SignalR.
/// Decouples the Infrastructure layer (which hosts <see cref="ConversionBackgroundTask"/>)
/// from the SignalR hub implementation in the main plugin project.
/// </summary>
public interface IProgressReporterFactory
{
    /// <summary>
    /// Creates a new progress reporter for a specific queue item.
    /// The returned <see cref="IProgress{ConversionProgress}"/> will broadcast progress
    /// updates to connected SignalR clients.
    /// </summary>
    /// <param name="queueItemId">The queue item ID to associate with progress updates.</param>
    /// <returns>A progress reporter that broadcasts via SignalR.</returns>
    IProgress<ConversionProgress> Create(Guid queueItemId);

    /// <summary>
    /// Broadcasts a conversion completed event to all connected SignalR clients.
    /// </summary>
    /// <param name="queueItemId">The queue item that completed.</param>
    /// <param name="status">The final conversion status.</param>
    /// <param name="conversionHistoryId">The conversion history record ID, if available.</param>
    Task SendCompletedAsync(Guid queueItemId, ConversionStatus status, Guid? conversionHistoryId);
}
