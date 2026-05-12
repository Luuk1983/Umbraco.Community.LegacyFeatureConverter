using Umbraco.Community.LegacyFeatureConverter.Models;
using Microsoft.Extensions.Logging;

namespace Umbraco.Community.LegacyFeatureConverter.Services;

/// <summary>
/// Service for managing conversion history and logging.
/// Stores conversion runs and detailed log entries in the database for audit and debugging.
/// Both real and test runs are logged to enable review and comparison.
/// </summary>
public interface IConversionHistoryService
{
    /// <summary>
    /// Starts a new conversion and creates a history record in the database.
    /// </summary>
    /// <param name="conversionId">The unique identifier for this conversion run.</param>
    /// <param name="converterType">The name of the converter being used.</param>
    /// <param name="isTestRun">Whether this is a test run (dry run).</param>
    /// <param name="selectedDocumentTypes">The document type keys that were selected, or null for all.</param>
    /// <param name="performingUserKey">The key of the Umbraco user performing the conversion.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task StartConversionAsync(
        Guid conversionId,
        string converterType,
        bool isTestRun,
        Guid[]? selectedDocumentTypes,
        Guid performingUserKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a single entry for the conversion.
    /// </summary>
    /// <param name="conversionId">The conversion ID this log belongs to.</param>
    /// <param name="level">The log level (Information, Warning, Error).</param>
    /// <param name="itemType">The type of item being logged (e.g., "DocumentType", "DataType", "Content", "Property").</param>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional additional details (JSON or plain text).</param>
    /// <param name="itemName">Optional name of the item being processed.</param>
    /// <param name="itemKey">Optional key/ID of the item being processed.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task LogEntryAsync(
        Guid conversionId,
        LogLevel level,
        string itemType,
        string message,
        string? details,
        string? itemName = null,
        string? itemKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a conversion and updates the history record with final results.
    /// </summary>
    /// <param name="conversionId">The conversion ID to complete.</param>
    /// <param name="result">The final conversion result containing all processed item details.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task CompleteConversionAsync(
        Guid conversionId,
        ConversionResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific conversion history record by its ID.
    /// </summary>
    /// <param name="conversionId">The conversion ID to retrieve.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The conversion history record, or null if not found.</returns>
    Task<ConversionHistory?> GetHistoryAsync(
        Guid conversionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of conversion history records, ordered by start date descending.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A paged result set of conversion history records.</returns>
    Task<PagedResult<ConversionHistory>> GetHistoryListAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all log entries for a specific conversion, ordered by timestamp.
    /// </summary>
    /// <param name="conversionId">The conversion ID to get logs for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All log entries for the conversion, ordered chronologically.</returns>
    Task<IEnumerable<ConversionLogEntry>> GetLogEntriesAsync(
        Guid conversionId,
        CancellationToken cancellationToken = default);
}
