using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Services;

/// <summary>
/// Service for managing the conversion job queue.
/// The queue is database-backed, so items survive application restarts.
/// </summary>
public interface IConversionQueueService
{
    /// <summary>
    /// Adds a new conversion job to the queue.
    /// </summary>
    /// <param name="options">The conversion options to queue.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The ID of the newly created queue item.</returns>
    Task<Guid> EnqueueAsync(
        ConversionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and marks the next queued item as running.
    /// Returns the oldest item with status <see cref="ConversionStatus.Queued"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The next queue item to process, or null if the queue is empty.</returns>
    Task<QueueItem?> DequeueAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all items currently in the queue, regardless of status.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All queue items, ordered by queued date.</returns>
    Task<IEnumerable<QueueItem>> GetQueueAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific queue item by its ID.
    /// </summary>
    /// <param name="id">The ID of the queue item.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The queue item, or null if not found.</returns>
    Task<QueueItem?> GetQueueItemAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a queued item. Only items with status <see cref="ConversionStatus.Queued"/>
    /// can be cancelled.
    /// </summary>
    /// <param name="id">The ID of the queue item to cancel.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>True if the item was cancelled, false if it was not found or not in a cancellable state.</returns>
    Task<bool> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a queue item as completed.
    /// </summary>
    /// <param name="id">The ID of the queue item.</param>
    /// <param name="status">The final status (Completed, CompletedWithErrors, or Failed).</param>
    /// <param name="conversionHistoryId">The ID of the associated conversion history record.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task CompleteQueueItemAsync(
        Guid id,
        ConversionStatus status,
        Guid? conversionHistoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets any items stuck in <see cref="ConversionStatus.Running"/> status back to
    /// <see cref="ConversionStatus.Queued"/>. Used for crash recovery on application startup.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The number of items that were reset.</returns>
    Task<int> ResetOrphanedItemsAsync(
        CancellationToken cancellationToken = default);
}
