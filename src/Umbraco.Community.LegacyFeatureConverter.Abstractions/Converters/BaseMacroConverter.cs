using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Base class for macro converters. Mirrors the lifecycle discipline of
/// <see cref="BasePropertyConverter"/>:
/// <list type="bullet">
///   <item>History tracking (start, log entries, complete) is wrapped around the body.</item>
///   <item>Cancellation is honored and surfaced as <see cref="ConversionStatus.Cancelled"/>.</item>
///   <item>Exceptions are logged and surfaced as <see cref="ConversionStatus.Failed"/>.</item>
///   <item><c>options.IsTestRun</c> is respected by concrete implementations — no saves during a dry run.</item>
/// </list>
///
/// Concrete converters implement <see cref="ExecuteCoreAsync"/> with the strategy-specific
/// phases (e.g. discover macros → create element types → configure RTE data types →
/// write stub partials → rewrite content).
/// </summary>
public abstract class BaseMacroConverter : IMacroConverter
{
    /// <summary>The logger instance for this converter.</summary>
    protected readonly ILogger _logger;

    /// <summary>The Umbraco macro service for resolving <c>IMacro</c> definitions by alias/key.</summary>
    protected readonly IMacroService _macroService;

    /// <summary>The Umbraco content type service for creating/querying element types.</summary>
    protected readonly IContentTypeService _contentTypeService;

    /// <summary>The Umbraco data type service for configuring rich-text data types with blocks.</summary>
    protected readonly IDataTypeService _dataTypeService;

    /// <summary>The Umbraco content service for scanning and rewriting rich-text content values.</summary>
    protected readonly IContentService _contentService;

    /// <summary>The Umbraco file service for writing generated stub partial views.</summary>
    protected readonly IFileService _fileService;

    /// <summary>The short-string helper for deriving safe element-type aliases from macro aliases.</summary>
    protected readonly IShortStringHelper _shortStringHelper;

    /// <summary>The conversion history service for audit logging.</summary>
    protected readonly IConversionHistoryService _historyService;

    /// <summary>The Umbraco scope provider for creating short-lived database scopes.</summary>
    protected readonly IScopeProvider _scopeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseMacroConverter"/> class.
    /// </summary>
    protected BaseMacroConverter(
        ILogger logger,
        IMacroService macroService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IContentService contentService,
        IFileService fileService,
        IShortStringHelper shortStringHelper,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _macroService = macroService ?? throw new ArgumentNullException(nameof(macroService));
        _contentTypeService = contentTypeService ?? throw new ArgumentNullException(nameof(contentTypeService));
        _dataTypeService = dataTypeService ?? throw new ArgumentNullException(nameof(dataTypeService));
        _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _shortStringHelper = shortStringHelper ?? throw new ArgumentNullException(nameof(shortStringHelper));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));
    }

    /// <inheritdoc />
    public abstract string ConverterName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public virtual string ShortName => ConverterName;

    /// <inheritdoc />
    public virtual string Category => "Macro";

    /// <inheritdoc />
    public virtual string Icon => "icon-code";

    /// <inheritdoc />
    public abstract string TargetShapeAlias { get; }

    /// <inheritdoc />
    public virtual async Task<ConversionResult> ExecuteConversionAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ConversionResult
        {
            ConversionId = Guid.NewGuid(),
            ConverterType = ConverterName,
            IsTestRun = options.IsTestRun,
            StartedAt = DateTime.UtcNow,
            Status = ConversionStatus.Running
        };

        try
        {
            await _historyService.StartConversionAsync(
                result.ConversionId,
                ConverterName,
                options.IsTestRun,
                options.SelectedMacroKeys,
                options.PerformingUserKey,
                cancellationToken);

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", $"Starting {ConverterName} conversion{(options.IsTestRun ? " (test run)" : "")}", null,
                cancellationToken: cancellationToken);

            await ExecuteCoreAsync(options, result, progress, cancellationToken);

            // Determine final status (mirrors BasePropertyConverter)
            result.Status = result.FailureCount > 0
                ? (result.SuccessCount > 0 ? ConversionStatus.CompletedWithErrors : ConversionStatus.Failed)
                : ConversionStatus.Completed;

            result.CompletedAt = DateTime.UtcNow;

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", options.IsTestRun
                    ? "Test run completed - no changes saved"
                    : "Conversion completed successfully", null,
                cancellationToken: cancellationToken);

            await _historyService.CompleteConversionAsync(result.ConversionId, result, cancellationToken);

            _logger.LogInformation("Macro conversion {ConversionId} completed with status {Status}",
                result.ConversionId, result.Status);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Macro conversion {ConversionId} was cancelled", result.ConversionId);

            result.Status = ConversionStatus.Cancelled;
            result.CompletedAt = DateTime.UtcNow;

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Warning,
                "Conversion", "Conversion was cancelled by the user", null);

            await _historyService.CompleteConversionAsync(result.ConversionId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during macro conversion {ConversionId}", result.ConversionId);

            result.Status = ConversionStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.StackTrace = ex.StackTrace;
            result.CompletedAt = DateTime.UtcNow;

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                "Conversion", $"Critical error: {ex.Message}", ex.StackTrace);

            await _historyService.CompleteConversionAsync(result.ConversionId, result);
        }

        return result;
    }

    /// <inheritdoc />
    public abstract Task<ConversionPlan> ComputePlanAsync(
        ConversionApproach approach,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Default <see cref="ILegacyFeatureConverter.GetAffectedUnitCountAsync"/> implementation:
    /// asks the converter for a fresh plan and counts the distinct macros it found in content.
    /// Concrete converters may override for a cheaper scan.
    /// </summary>
    public virtual async Task<int> GetAffectedUnitCountAsync(CancellationToken cancellationToken = default)
    {
        var plan = await ComputePlanAsync(ConversionApproach.Fast, cancellationToken);
        return plan.Macros.Count;
    }

    /// <summary>
    /// Hook: the macro-specific conversion work. Called by <see cref="ExecuteConversionAsync"/>
    /// inside the history/exception wrapper. Implementations should populate <paramref name="result"/>
    /// (SuccessCount, FailureCount, content/data-type lists) so the lifecycle wrapper can derive the
    /// final <see cref="ConversionStatus"/>.
    /// </summary>
    /// <param name="options">The conversion options.</param>
    /// <param name="result">The result to populate as work progresses.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    protected abstract Task ExecuteCoreAsync(
        ConversionOptions options,
        ConversionResult result,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken);
}
