using LP.Umbraco.LegacyFeatureConverter.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LP.Umbraco.LegacyFeatureConverter.Hubs;

/// <summary>
/// Bridges the <see cref="IProgress{ConversionProgress}"/> interface used by converters
/// to the SignalR hub for real-time progress broadcasting.
///
/// Throttles rapid updates to a maximum of one broadcast per <see cref="ThrottleInterval"/>
/// to avoid flooding SignalR connections during content conversion (which can process
/// hundreds of items). The first update is always sent immediately, and
/// <see cref="FlushAsync"/> sends any pending throttled update.
/// </summary>
public class SignalRProgressReporter : IProgress<ConversionProgress>
{
    private readonly IHubContext<ConversionHub, IConversionHubClient> _hubContext;
    private readonly Guid _queueItemId;
    private readonly ILogger<SignalRProgressReporter> _logger;

    private DateTime _lastSentAt = DateTime.MinValue;
    private ConversionProgress? _pendingProgress;
    private readonly object _lock = new();

    /// <summary>
    /// Minimum interval between SignalR broadcasts.
    /// </summary>
    internal static readonly TimeSpan ThrottleInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRProgressReporter"/> class.
    /// </summary>
    /// <param name="hubContext">The typed SignalR hub context for broadcasting.</param>
    /// <param name="queueItemId">The queue item ID to associate with all progress updates.</param>
    /// <param name="logger">The logger instance.</param>
    public SignalRProgressReporter(
        IHubContext<ConversionHub, IConversionHubClient> hubContext,
        Guid queueItemId,
        ILogger<SignalRProgressReporter> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _queueItemId = queueItemId;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reports a progress update. Sets <see cref="ConversionProgress.QueueItemId"/>
    /// and broadcasts via SignalR, throttled to avoid flooding.
    /// </summary>
    /// <param name="value">The progress to report.</param>
    public void Report(ConversionProgress value)
    {
        value.QueueItemId = _queueItemId;

        bool shouldSend;

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastSentAt >= ThrottleInterval)
            {
                _lastSentAt = now;
                _pendingProgress = null;
                shouldSend = true;
            }
            else
            {
                // Throttled: store as pending for later flush
                _pendingProgress = value;
                shouldSend = false;
            }
        }

        if (shouldSend)
        {
            _ = SendProgressAsync(value);
        }
    }

    /// <summary>
    /// Broadcasts a conversion completed event to all connected clients.
    /// </summary>
    /// <param name="status">The final conversion status.</param>
    /// <param name="conversionHistoryId">The history record ID, if available.</param>
    public async Task SendCompletedAsync(ConversionStatus status, Guid? conversionHistoryId)
    {
        try
        {
            await _hubContext.Clients.All.ConversionCompleted(
                _queueItemId, status, conversionHistoryId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Legacy Feature Converter: Failed to broadcast conversion completed for queue item {QueueItemId}",
                _queueItemId);
        }
    }

    /// <summary>
    /// Sends any pending throttled progress update.
    /// Call this before completion to ensure the last progress state is sent.
    /// </summary>
    public async Task FlushAsync()
    {
        ConversionProgress? pending;

        lock (_lock)
        {
            pending = _pendingProgress;
            _pendingProgress = null;
        }

        if (pending != null)
        {
            await SendProgressAsync(pending);
        }
    }

    private async Task SendProgressAsync(ConversionProgress progress)
    {
        try
        {
            await _hubContext.Clients.All.ReceiveProgress(progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Legacy Feature Converter: Failed to broadcast progress for queue item {QueueItemId}",
                _queueItemId);
        }
    }
}
