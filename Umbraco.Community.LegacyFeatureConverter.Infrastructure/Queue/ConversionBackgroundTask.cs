using System.Text.Json;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.LegacyFeatureConverter.Infrastructure.Queue;

/// <summary>
/// Background service that processes the conversion queue.
/// Polls the queue every <see cref="PollingInterval"/> and executes conversions one at a time.
///
/// On startup, resets any orphaned items (stuck in Running) back to Queued for crash recovery.
/// Supports <see cref="ConversionOptions.RunTestFirst"/>: runs a test conversion first,
/// and only proceeds with the real conversion if the test succeeds.
///
/// Uses <see cref="IServiceScopeFactory"/> to create a new DI scope per conversion,
/// ensuring that scoped services (DbContext, Umbraco services) are properly disposed.
/// </summary>
public class ConversionBackgroundTask : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ConversionBackgroundTask> _logger;

    /// <summary>
    /// The interval between queue polling attempts.
    /// </summary>
    internal static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionBackgroundTask"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">Factory for creating DI scopes.</param>
    /// <param name="logger">The logger instance.</param>
    public ConversionBackgroundTask(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ConversionBackgroundTask> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the background task loop: reset orphaned items, then poll and process the queue.
    /// </summary>
    /// <param name="stoppingToken">Token that signals when the host is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Legacy Feature Converter: Background task started");

        // Crash recovery: reset any items stuck in Running state
        await ResetOrphanedItemsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextQueueItemAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Legacy Feature Converter: Error in background task loop");
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Legacy Feature Converter: Background task stopped");
    }

    /// <summary>
    /// Resets any queue items stuck in Running state (from a previous crash) back to Queued.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task ResetOrphanedItemsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<IConversionQueueService>();
            var resetCount = await queueService.ResetOrphanedItemsAsync(cancellationToken);

            if (resetCount > 0)
            {
                _logger.LogWarning(
                    "Legacy Feature Converter: Reset {Count} orphaned queue items on startup",
                    resetCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Legacy Feature Converter: Error resetting orphaned queue items");
        }
    }

    /// <summary>
    /// Dequeues and processes the next item in the queue, if any.
    /// Creates a new DI scope for each conversion to ensure clean service lifetimes.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task ProcessNextQueueItemAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IConversionQueueService>();

        var queueItem = await queueService.DequeueAsync(cancellationToken);
        if (queueItem == null)
            return; // Queue is empty

        _logger.LogInformation(
            "Legacy Feature Converter: Processing queue item {QueueItemId}",
            queueItem.Id);

        var converterService = scope.ServiceProvider.GetRequiredService<IConverterService>();
        var progressFactory = scope.ServiceProvider.GetRequiredService<IProgressReporterFactory>();
        var options = DeserializeOptions(queueItem.SerializedOptions);

        if (options == null)
        {
            _logger.LogError(
                "Legacy Feature Converter: Failed to deserialize options for queue item {QueueItemId}",
                queueItem.Id);
            await queueService.CompleteQueueItemAsync(
                queueItem.Id, ConversionStatus.Failed, cancellationToken: cancellationToken);
            await progressFactory.SendCompletedAsync(queueItem.Id, ConversionStatus.Failed, null);
            return;
        }

        var converter = converterService.GetConverterByName(options.ConverterType);
        if (converter == null)
        {
            _logger.LogError(
                "Legacy Feature Converter: Converter '{ConverterType}' not found for queue item {QueueItemId}",
                options.ConverterType, queueItem.Id);
            await queueService.CompleteQueueItemAsync(
                queueItem.Id, ConversionStatus.Failed, cancellationToken: cancellationToken);
            await progressFactory.SendCompletedAsync(queueItem.Id, ConversionStatus.Failed, null);
            return;
        }

        // If RunTestFirst is enabled, run a test conversion first
        if (options.RunTestFirst && !options.IsTestRun)
        {
            var testPassed = await RunTestConversionAsync(
                converter, options, queueItem.Id, queueService, progressFactory, cancellationToken);

            if (!testPassed)
            {
                _logger.LogWarning(
                    "Legacy Feature Converter: Test run failed for queue item {QueueItemId}, skipping actual conversion",
                    queueItem.Id);
                return; // Queue item already marked as failed by RunTestConversionAsync
            }
        }

        // Execute the actual conversion
        await ExecuteConversionAsync(
            converter, options, queueItem.Id, queueService, progressFactory, cancellationToken);
    }

    /// <summary>
    /// Runs a test (dry-run) conversion to validate before the actual conversion.
    /// </summary>
    /// <param name="converter">The converter to use.</param>
    /// <param name="options">The original conversion options.</param>
    /// <param name="queueItemId">The queue item ID for status updates.</param>
    /// <param name="queueService">The queue service for status updates.</param>
    /// <param name="progressFactory">Factory for creating SignalR progress reporters.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>True if the test run completed without failures, false otherwise.</returns>
    private async Task<bool> RunTestConversionAsync(
        Converters.IPropertyConverter converter,
        ConversionOptions options,
        Guid queueItemId,
        IConversionQueueService queueService,
        IProgressReporterFactory progressFactory,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Legacy Feature Converter: Running test conversion for queue item {QueueItemId}",
            queueItemId);

        var testOptions = new ConversionOptions
        {
            ConverterType = options.ConverterType,
            SelectedDocumentTypeKeys = options.SelectedDocumentTypeKeys,
            IsTestRun = true,
            StopOnError = options.StopOnError,
            RunTestFirst = false, // Prevent infinite recursion
            PerformingUserKey = options.PerformingUserKey
        };

        var progress = progressFactory.Create(queueItemId);
        var testResult = await converter.ExecuteConversionAsync(
            testOptions, progress: progress, cancellationToken: cancellationToken);

        if (testResult.Status == ConversionStatus.Failed)
        {
            _logger.LogError(
                "Legacy Feature Converter: Test run failed for queue item {QueueItemId}: {Error}",
                queueItemId, testResult.ErrorMessage);

            await queueService.CompleteQueueItemAsync(
                queueItemId, ConversionStatus.Failed,
                testResult.ConversionId, cancellationToken);

            await progressFactory.SendCompletedAsync(
                queueItemId, ConversionStatus.Failed, testResult.ConversionId);

            return false;
        }

        _logger.LogInformation(
            "Legacy Feature Converter: Test run succeeded for queue item {QueueItemId}",
            queueItemId);

        return true;
    }

    /// <summary>
    /// Executes the actual conversion and updates the queue item status.
    /// </summary>
    /// <param name="converter">The converter to use.</param>
    /// <param name="options">The conversion options.</param>
    /// <param name="queueItemId">The queue item ID for status updates.</param>
    /// <param name="queueService">The queue service for status updates.</param>
    /// <param name="progressFactory">Factory for creating SignalR progress reporters.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task ExecuteConversionAsync(
        Converters.IPropertyConverter converter,
        ConversionOptions options,
        Guid queueItemId,
        IConversionQueueService queueService,
        IProgressReporterFactory progressFactory,
        CancellationToken cancellationToken)
    {
        var progress = progressFactory.Create(queueItemId);

        try
        {
            var result = await converter.ExecuteConversionAsync(
                options, progress: progress, cancellationToken: cancellationToken);

            await queueService.CompleteQueueItemAsync(
                queueItemId, result.Status, result.ConversionId, cancellationToken);

            await progressFactory.SendCompletedAsync(
                queueItemId, result.Status, result.ConversionId);

            _logger.LogInformation(
                "Legacy Feature Converter: Queue item {QueueItemId} completed with status {Status}",
                queueItemId, result.Status);
        }
        catch (OperationCanceledException)
        {
            await queueService.CompleteQueueItemAsync(
                queueItemId, ConversionStatus.Cancelled, cancellationToken: default);

            await progressFactory.SendCompletedAsync(
                queueItemId, ConversionStatus.Cancelled, null);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Legacy Feature Converter: Queue item {QueueItemId} failed with error",
                queueItemId);

            await queueService.CompleteQueueItemAsync(
                queueItemId, ConversionStatus.Failed, cancellationToken: default);

            await progressFactory.SendCompletedAsync(
                queueItemId, ConversionStatus.Failed, null);
        }
    }

    /// <summary>
    /// Deserializes the JSON-serialized conversion options from the queue item.
    /// </summary>
    /// <param name="serializedOptions">The JSON string.</param>
    /// <returns>The deserialized options, or null if deserialization fails.</returns>
    private ConversionOptions? DeserializeOptions(string serializedOptions)
    {
        try
        {
            return JsonSerializer.Deserialize<ConversionOptions>(serializedOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize conversion options: {Json}", serializedOptions);
            return null;
        }
    }
}
