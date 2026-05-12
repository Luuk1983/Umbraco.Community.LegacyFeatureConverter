using System.Text.Json;
using Umbraco.Community.LegacyFeatureConverter.Data;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;

/// <summary>
/// Database-backed FIFO queue service for conversion jobs.
/// Items are persisted to the database, so the queue survives application restarts.
/// </summary>
public class ConversionQueueService : IConversionQueueService
{
    private readonly LegacyFeatureConverterDbContext _dbContext;
    private readonly ILogger<ConversionQueueService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionQueueService"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ConversionQueueService(
        LegacyFeatureConverterDbContext dbContext,
        ILogger<ConversionQueueService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueAsync(
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Queued
        };

        _dbContext.QueueItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enqueued conversion {QueueItemId} for converter '{ConverterType}'",
            item.Id, options.ConverterType);

        return item.Id;
    }

    /// <inheritdoc />
    public async Task<QueueItem?> DequeueAsync(
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.QueueItems
            .Where(q => q.Status == ConversionStatus.Queued)
            .OrderBy(q => q.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (item == null)
            return null;

        item.Status = ConversionStatus.Running;
        item.StartedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Dequeued conversion {QueueItemId}", item.Id);

        return item;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QueueItem>> GetQueueAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueueItems
            .AsNoTracking()
            .OrderBy(q => q.QueuedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QueueItem?> GetQueueItemAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueueItems
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.QueueItems
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (item == null || item.Status != ConversionStatus.Queued)
        {
            _logger.LogWarning(
                "Cannot cancel queue item {QueueItemId}: not found or not in Queued state (current: {Status})",
                id, item?.Status);
            return false;
        }

        item.Status = ConversionStatus.Cancelled;
        item.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cancelled queue item {QueueItemId}", id);

        return true;
    }

    /// <inheritdoc />
    public async Task CompleteQueueItemAsync(
        Guid id,
        ConversionStatus status,
        Guid? conversionHistoryId = null,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.QueueItems
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (item == null)
        {
            _logger.LogWarning("Queue item {QueueItemId} not found for completion", id);
            return;
        }

        item.Status = status;
        item.CompletedAt = DateTime.UtcNow;
        item.ConversionHistoryId = conversionHistoryId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Completed queue item {QueueItemId} with status {Status}",
            id, status);
    }

    /// <inheritdoc />
    public async Task<int> ResetOrphanedItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var orphanedItems = await _dbContext.QueueItems
            .Where(q => q.Status == ConversionStatus.Running)
            .ToListAsync(cancellationToken);

        if (orphanedItems.Count == 0)
            return 0;

        foreach (var item in orphanedItems)
        {
            item.Status = ConversionStatus.Queued;
            item.StartedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Reset {Count} orphaned queue items from Running to Queued (crash recovery)",
            orphanedItems.Count);

        return orphanedItems.Count;
    }
}
