using System.Text.Json;
using Umbraco.Community.LegacyFeatureConverter.Data;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;

/// <summary>
/// Service for managing conversion history and logging using Entity Framework Core.
/// Provides methods to create, update, and query conversion records and log entries.
/// Both real and test runs are logged to enable review and audit.
/// </summary>
public class ConversionHistoryService : IConversionHistoryService
{
    private readonly LegacyFeatureConverterDbContext _dbContext;
    private readonly ILogger<ConversionHistoryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionHistoryService"/> class.
    /// </summary>
    /// <param name="dbContext">The EF Core database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ConversionHistoryService(
        LegacyFeatureConverterDbContext dbContext,
        ILogger<ConversionHistoryService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartConversionAsync(
        Guid conversionId,
        string converterType,
        bool isTestRun,
        Guid[]? selectedDocumentTypes,
        Guid performingUserKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = new ConversionHistory
            {
                Id = conversionId,
                StartedAt = DateTime.UtcNow,
                ConverterType = converterType,
                IsTestRun = isTestRun,
                Status = "Running",
                SelectedDocumentTypes = selectedDocumentTypes != null
                    ? JsonSerializer.Serialize(selectedDocumentTypes)
                    : null,
                PerformingUserKey = performingUserKey
            };

            _dbContext.ConversionHistories.Add(history);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Started conversion {ConversionId} ({ConverterType}) - Test Run: {IsTestRun}",
                conversionId, converterType, isTestRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start conversion history for {ConversionId}", conversionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task LogEntryAsync(
        Guid conversionId,
        LogLevel level,
        string itemType,
        string message,
        string? details,
        string? itemName = null,
        string? itemKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new ConversionLogEntry
            {
                Id = Guid.NewGuid(),
                ConversionHistoryId = conversionId,
                Timestamp = DateTime.UtcNow,
                Level = level.ToString(),
                ItemType = itemType,
                ItemName = itemName,
                ItemKey = itemKey,
                Message = message,
                Details = details,
                StackTrace = level == LogLevel.Error ? details : null
            };

            _dbContext.ConversionLogs.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.Log(level, "{ItemType}: {Message}", itemType, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add log entry for conversion {ConversionId}", conversionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CompleteConversionAsync(
        Guid conversionId,
        ConversionResult result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _dbContext.ConversionHistories.FindAsync(
                new object[] { conversionId }, cancellationToken);

            if (history == null)
            {
                _logger.LogWarning("Conversion history {ConversionId} not found", conversionId);
                return;
            }

            history.CompletedAt = DateTime.UtcNow;
            history.Status = result.Status.ToString();
            history.TotalDocumentTypes = result.DocumentTypes.Count;
            history.TotalDataTypes = result.DataTypes.Count;
            history.TotalContentNodes = result.ContentNodes.Count;
            history.SuccessCount = result.SuccessCount;
            history.FailureCount = result.FailureCount;
            history.SkippedCount = result.SkippedCount;
            history.Summary = JsonSerializer.Serialize(new
            {
                result.Status,
                result.StartedAt,
                result.CompletedAt,
                result.Duration,
                DocumentTypes = result.DocumentTypes.Select(dt => new
                {
                    dt.Key, dt.Name, dt.Alias, dt.Success, dt.Skipped,
                    dt.PropertiesUpdated, dt.ErrorMessage
                }),
                DataTypes = result.DataTypes.Select(dt => new
                {
                    dt.Id, dt.Name, dt.Key, dt.Success, dt.Skipped, dt.ErrorMessage
                }),
                ContentNodes = result.ContentNodes.Select(cn => new
                {
                    cn.Id, cn.Key, cn.Name, cn.Success, cn.Skipped,
                    cn.PropertiesConverted, cn.ErrorMessage
                })
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Completed conversion {ConversionId} with status {Status} - Test Run: {IsTestRun}",
                conversionId, result.Status, result.IsTestRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete conversion history for {ConversionId}", conversionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ConversionHistory?> GetHistoryAsync(
        Guid conversionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ConversionHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == conversionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversion history {ConversionId}", conversionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<ConversionHistory>> GetHistoryListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbContext.ConversionHistories.AsNoTracking();
            var totalItems = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(h => h.StartedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<ConversionHistory>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversion history list");

            return new PagedResult<ConversionHistory>
            {
                Items = [],
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = 0
            };
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConversionLogEntry>> GetLogEntriesAsync(
        Guid conversionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.ConversionLogs
                .AsNoTracking()
                .Where(l => l.ConversionHistoryId == conversionId)
                .OrderBy(l => l.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get log entries for conversion {ConversionId}", conversionId);
            return Enumerable.Empty<ConversionLogEntry>();
        }
    }
}
