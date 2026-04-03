using System.Text.Json;
using System.Text.Json.Serialization;
using LP.Umbraco.LegacyFeatureConverter.Dtos;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Controllers;

namespace LP.Umbraco.LegacyFeatureConverter.Controllers;

/// <summary>
/// API controller for the Legacy Feature Converter backoffice section.
/// Provides endpoints for converter discovery, document type selection,
/// queue management, and conversion history.
///
/// All endpoints require backoffice authentication and Settings section access.
/// Conversions are queued for background processing rather than executed synchronously.
/// </summary>
[IsBackOffice]
[PluginController("LegacyFeatureConverter")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public class LegacyConverterApiController : UmbracoApiController
{
    private readonly IConverterService _converterService;
    private readonly IConversionHistoryService _historyService;
    private readonly IConversionQueueService _queueService;
    private readonly ILogger<LegacyConverterApiController> _logger;

    /// <summary>
    /// JSON serializer options with enum-as-string and camelCase for JavaScript compatibility.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyConverterApiController"/> class.
    /// </summary>
    /// <param name="converterService">Service for discovering converters.</param>
    /// <param name="historyService">Service for conversion history and logs.</param>
    /// <param name="queueService">Service for the conversion queue.</param>
    /// <param name="logger">The logger instance.</param>
    public LegacyConverterApiController(
        IConverterService converterService,
        IConversionHistoryService historyService,
        IConversionQueueService queueService,
        ILogger<LegacyConverterApiController> logger)
    {
        _converterService = converterService;
        _historyService = historyService;
        _queueService = queueService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available converters with metadata (name, description, affected count).
    /// </summary>
    /// <returns>List of converter metadata.</returns>
    [HttpGet]
    public async Task<IActionResult> GetConverters()
    {
        try
        {
            var metadata = await _converterService.GetConverterMetadataAsync();
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting converters");
            return StatusCode(500, new { error = "Failed to get converters", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets document types that would be affected by a specific converter.
    /// </summary>
    /// <param name="converterName">The name of the converter.</param>
    /// <returns>List of affected document types with property counts.</returns>
    [HttpGet]
    public async Task<IActionResult> GetDocumentTypes(string converterName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(converterName))
                return BadRequest(new { error = "Converter name is required" });

            var converter = _converterService.GetConverterByName(converterName);
            if (converter == null)
                return NotFound(new { error = $"Converter '{converterName}' not found" });

            var documentTypes = await _converterService.GetAffectedDocumentTypesAsync(converterName);
            return Ok(documentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document types for converter {ConverterName}", converterName);
            return StatusCode(500, new { error = "Failed to get document types", details = ex.Message });
        }
    }

    /// <summary>
    /// Queues a new conversion for background processing.
    /// Returns immediately with the queue item ID.
    /// </summary>
    /// <param name="request">The conversion request parameters.</param>
    /// <returns>The ID of the queued conversion.</returns>
    [HttpPost]
    public async Task<IActionResult> QueueConversion([FromBody] ConversionRequestDto? request)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            if (string.IsNullOrWhiteSpace(request.ConverterType))
                return BadRequest(new { error = "Converter type is required" });

            var converter = _converterService.GetConverterByName(request.ConverterType);
            if (converter == null)
                return NotFound(new { error = $"Converter '{request.ConverterType}' not found" });

            var options = new ConversionOptions
            {
                ConverterType = request.ConverterType,
                SelectedDocumentTypeKeys = request.SelectedDocumentTypeKeys,
                IsTestRun = request.IsTestRun,
                StopOnError = request.StopOnError,
                RunTestFirst = request.RunTestFirst,
                PublishAfterConversion = request.PublishAfterConversion,
                PerformingUserKey = GetCurrentUserKey()
            };

            var queueItemId = await _queueService.EnqueueAsync(options);

            _logger.LogInformation(
                "Queued conversion {QueueItemId}: {ConverterType}, TestRun: {IsTestRun}",
                queueItemId, options.ConverterType, options.IsTestRun);

            return Ok(new { queueItemId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing conversion");
            return StatusCode(500, new { error = "Failed to queue conversion", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the active conversion queue — only items that are Queued or Running.
    /// Completed, failed, and cancelled items are excluded from this view.
    /// </summary>
    /// <returns>Active queue items ordered by queued date.</returns>
    [HttpGet]
    public async Task<IActionResult> GetQueueStatus()
    {
        try
        {
            var queue = await _queueService.GetQueueAsync();
            var activeQueue = queue.Where(q =>
                q.Status == ConversionStatus.Queued ||
                q.Status == ConversionStatus.Running);
            return new JsonResult(activeQueue, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue status");
            return StatusCode(500, new { error = "Failed to get queue status", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a queued conversion. Only items in Queued state can be cancelled.
    /// </summary>
    /// <param name="id">The ID of the queue item to cancel.</param>
    /// <returns>Success or failure indication.</returns>
    [HttpDelete]
    public async Task<IActionResult> CancelConversion(Guid id)
    {
        try
        {
            var cancelled = await _queueService.CancelAsync(id);
            if (!cancelled)
                return BadRequest(new { error = "Cannot cancel: item not found or not in Queued state" });

            _logger.LogInformation("Cancelled queue item {QueueItemId}", id);
            return Ok(new { cancelled = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling conversion {Id}", id);
            return StatusCode(500, new { error = "Failed to cancel conversion", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets paginated conversion history, ordered by start date descending.
    /// </summary>
    /// <param name="page">The page number (1-based, default 1).</param>
    /// <param name="pageSize">The number of items per page (default 20, max 100).</param>
    /// <returns>A paged result of conversion history records.</returns>
    [HttpGet]
    public async Task<IActionResult> GetHistory(int page = 1, int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var history = await _historyService.GetHistoryListAsync(page, pageSize);
            return new JsonResult(history, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversion history");
            return StatusCode(500, new { error = "Failed to get history", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets detailed information and log entries for a specific conversion.
    /// </summary>
    /// <param name="id">The conversion history ID.</param>
    /// <returns>The conversion history record and its log entries.</returns>
    [HttpGet]
    public async Task<IActionResult> GetConversionDetails(Guid id)
    {
        try
        {
            var history = await _historyService.GetHistoryAsync(id);
            if (history == null)
                return NotFound(new { error = "Conversion not found" });

            var logs = await _historyService.GetLogEntriesAsync(id);

            return new JsonResult(new { history, logs }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversion details for {ConversionId}", id);
            return StatusCode(500, new { error = "Failed to get conversion details", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the key of the current backoffice user from the security context.
    /// Falls back to Guid.Empty if the user key cannot be determined.
    /// </summary>
    /// <returns>The current user's key.</returns>
    private Guid GetCurrentUserKey()
    {
        var userIdClaim = HttpContext?.User?.FindFirst("sub")
            ?? HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userKey))
            return userKey;

        return Guid.Empty;
    }
}
