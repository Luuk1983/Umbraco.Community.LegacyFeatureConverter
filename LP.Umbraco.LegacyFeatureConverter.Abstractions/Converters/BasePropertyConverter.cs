using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace LP.Umbraco.LegacyFeatureConverter.Converters;

/// <summary>
/// Base class for property editor converters providing a standard 4-phase conversion workflow.
/// Derived classes implement specific conversion logic by overriding
/// <see cref="CreateTargetDataTypeAsync"/> and <see cref="ConvertPropertyValueAsync"/>.
///
/// The 4 phases are:
/// 1. Scan document types for properties using the source property editors
/// 2. Create target data types from source data types
/// 3. Update property definitions in document types (including compositions)
/// 4. Convert content node property values from old to new format
///
/// Each database save uses a short-lived scope to prevent lock timeouts.
/// For test runs, scopes are created but not completed, causing automatic rollback.
/// </summary>
public abstract class BasePropertyConverter : IPropertyConverter
{
    /// <summary>
    /// The logger instance for this converter.
    /// </summary>
    protected readonly ILogger _logger;

    /// <summary>
    /// The Umbraco data type service for querying and saving data types.
    /// </summary>
    protected readonly IDataTypeService _dataTypeService;

    /// <summary>
    /// The Umbraco content type service for querying and saving document types.
    /// </summary>
    protected readonly IContentTypeService _contentTypeService;

    /// <summary>
    /// The Umbraco content service for querying and saving content nodes.
    /// </summary>
    protected readonly IContentService _contentService;

    /// <summary>
    /// The conversion history service for audit logging.
    /// </summary>
    protected readonly IConversionHistoryService _historyService;

    /// <summary>
    /// The Umbraco scope provider for creating short-lived database scopes.
    /// </summary>
    protected readonly IScopeProvider _scopeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasePropertyConverter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dataTypeService">The Umbraco data type service.</param>
    /// <param name="contentTypeService">The Umbraco content type service.</param>
    /// <param name="contentService">The Umbraco content service.</param>
    /// <param name="historyService">The conversion history service.</param>
    /// <param name="scopeProvider">The Umbraco scope provider.</param>
    protected BasePropertyConverter(
        ILogger logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataTypeService = dataTypeService ?? throw new ArgumentNullException(nameof(dataTypeService));
        _contentTypeService = contentTypeService ?? throw new ArgumentNullException(nameof(contentTypeService));
        _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));
    }

    /// <inheritdoc />
    public abstract string ConverterName { get; }

    /// <inheritdoc />
    public abstract string[] SourcePropertyEditorAliases { get; }

    /// <inheritdoc />
    public abstract string TargetPropertyEditorAlias { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

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
            // Start history tracking
            await _historyService.StartConversionAsync(
                result.ConversionId,
                ConverterName,
                options.IsTestRun,
                options.SelectedDocumentTypeKeys,
                options.PerformingUserKey,
                cancellationToken);

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", $"Starting {ConverterName} conversion{(options.IsTestRun ? " (test run)" : "")}", null,
                cancellationToken: cancellationToken);

            // Phase 1: Scan document types for properties using source property editors
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", "Phase 1: Scanning document types", null,
                cancellationToken: cancellationToken);

            ReportProgress(progress, result.ConversionId, "Scanning document types", "", 0, 0);

            var documentTypes = await ScanDocumentTypesAsync(options.SelectedDocumentTypeKeys, cancellationToken);
            result.DocumentTypes.AddRange(documentTypes.Select(dt => new DocumentTypeConversionInfo
            {
                Key = dt.Key,
                Name = dt.Name,
                Alias = dt.Alias
            }));

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", $"Found {documentTypes.Count} document types to process", null,
                cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Create or get target data types
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", "Phase 2: Creating target data types", null,
                cancellationToken: cancellationToken);

            var dataTypeMap = await CreateOrGetTargetDataTypesAsync(result, documentTypes, options.IsTestRun, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Track property aliases BEFORE Phase 3 modifies the document types
            var propertyAliasesToConvert = TrackPropertyAliases(documentTypes);

            // Phase 3: Update property types in document types
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", "Phase 3: Updating document type properties", null,
                cancellationToken: cancellationToken);

            await UpdateDocumentTypePropertiesAsync(result, documentTypes, dataTypeMap, options.IsTestRun, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 4: Convert content node data
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", "Phase 4: Converting content node data", null,
                cancellationToken: cancellationToken);

            await ConvertContentDataAsync(result, documentTypes, propertyAliasesToConvert, options, progress, cancellationToken);

            // Determine final status
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

            _logger.LogInformation("Conversion {ConversionId} completed with status {Status}",
                result.ConversionId, result.Status);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Conversion {ConversionId} was cancelled", result.ConversionId);

            result.Status = ConversionStatus.Cancelled;
            result.CompletedAt = DateTime.UtcNow;

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Warning,
                "Conversion", "Conversion was cancelled by the user", null);

            await _historyService.CompleteConversionAsync(result.ConversionId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during conversion {ConversionId}", result.ConversionId);

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
    public virtual async Task<int> GetAffectedDocumentTypesCountAsync(
        Guid[]? selectedDocumentTypeKeys = null,
        CancellationToken cancellationToken = default)
    {
        var documentTypes = await ScanDocumentTypesAsync(selectedDocumentTypeKeys, cancellationToken);
        return documentTypes.Count;
    }

    /// <summary>
    /// Scans all document types (or a filtered set) for properties using the source property editors.
    /// Checks both direct properties and composition properties.
    /// </summary>
    /// <param name="selectedDocumentTypeKeys">Optional filter for specific document type keys.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>List of document types that have properties using the source property editors.</returns>
    protected virtual Task<List<IContentType>> ScanDocumentTypesAsync(
        Guid[]? selectedDocumentTypeKeys,
        CancellationToken cancellationToken = default)
    {
        var allDocumentTypes = _contentTypeService.GetAll().ToList();

        if (selectedDocumentTypeKeys is { Length: > 0 })
        {
            allDocumentTypes = allDocumentTypes
                .Where(dt => selectedDocumentTypeKeys.Contains(dt.Key))
                .ToList();
        }

        var affectedDocumentTypes = new List<IContentType>();

        foreach (var docType in allDocumentTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hasSourceProperties =
                docType.PropertyTypes.Any(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias)) ||
                docType.CompositionPropertyTypes.Any(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias));

            if (hasSourceProperties)
            {
                affectedDocumentTypes.Add(docType);
            }
        }

        return Task.FromResult(affectedDocumentTypes);
    }

    /// <summary>
    /// Creates or retrieves target data types for all source data types found across the document types.
    /// Returns a mapping of source data type ID to target data type.
    /// Each data type save uses its own short-lived scope to avoid database lock timeouts.
    /// </summary>
    /// <param name="result">The conversion result to populate with data type info.</param>
    /// <param name="documentTypes">The document types to scan for source data types.</param>
    /// <param name="isTestRun">Whether this is a test run (no saves).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A mapping of source data type ID to target data type.</returns>
    protected virtual async Task<Dictionary<int, IDataType>> CreateOrGetTargetDataTypesAsync(
        ConversionResult result,
        List<IContentType> documentTypes,
        bool isTestRun,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var dataTypeMap = new Dictionary<int, IDataType>();
        var processedDataTypeIds = new HashSet<int>();

        foreach (var docType in documentTypes)
        {
            var allProperties = docType.PropertyTypes
                .Concat(docType.CompositionPropertyTypes)
                .Where(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias))
                .ToList();

            foreach (var property in allProperties)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!processedDataTypeIds.Add(property.DataTypeId))
                    continue;

                var conversionInfo = new DataTypeConversionInfo
                {
                    Id = property.DataTypeId,
                    Name = property.Name
                };

                try
                {
                    var sourceDataType = _dataTypeService.GetDataType(property.DataTypeId);
                    if (sourceDataType == null)
                    {
                        conversionInfo.ErrorMessage = "Source data type not found";
                        result.DataTypes.Add(conversionInfo);
                        continue;
                    }

                    conversionInfo.Name = sourceDataType.Name;
                    conversionInfo.Key = sourceDataType.Key;

                    ReportProgress(progress, result.ConversionId, "Creating data types",
                        sourceDataType.Name, processedDataTypeIds.Count, processedDataTypeIds.Count);

                    var targetDataType = await CreateTargetDataTypeAsync(sourceDataType);
                    if (targetDataType != null)
                    {
                        var existing = _dataTypeService.GetDataType(targetDataType.Name);
                        if (existing == null)
                        {
                            if (!isTestRun)
                            {
                                _dataTypeService.Save(targetDataType);
                            }

                            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                                "DataType",
                                $"{(isTestRun ? "[DRY RUN] Would create" : "Created")} data type: {targetDataType.Name}",
                                null, targetDataType.Name, sourceDataType.Key.ToString(),
                                cancellationToken: cancellationToken);
                        }
                        else
                        {
                            targetDataType = existing;
                            conversionInfo.Skipped = true;

                            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                                "DataType", $"Data type already exists: {existing.Name}",
                                null, existing.Name, existing.Key.ToString(),
                                cancellationToken: cancellationToken);
                        }

                        dataTypeMap[property.DataTypeId] = targetDataType;
                        conversionInfo.Success = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating data type for {PropertyName}", property.Name);
                    conversionInfo.ErrorMessage = ex.Message;

                    await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                        "DataType", $"Error creating data type for {property.Name}: {ex.Message}",
                        ex.StackTrace, property.Name, cancellationToken: cancellationToken);
                }

                result.DataTypes.Add(conversionInfo);
            }
        }

        return dataTypeMap;
    }

    /// <summary>
    /// Updates property types in document types to use the target data types.
    /// Handles both direct properties and composition properties.
    /// Tracks already-updated compositions to prevent duplicate saves.
    /// Each document type save uses its own short-lived scope.
    /// </summary>
    /// <param name="result">The conversion result to populate.</param>
    /// <param name="documentTypes">The document types to update.</param>
    /// <param name="dataTypeMap">Mapping of source data type ID to target data type.</param>
    /// <param name="isTestRun">Whether this is a test run (scopes not completed).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    protected virtual async Task UpdateDocumentTypePropertiesAsync(
        ConversionResult result,
        List<IContentType> documentTypes,
        Dictionary<int, IDataType> dataTypeMap,
        bool isTestRun,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var updatedCompositionIds = new HashSet<int>();
        var processedCount = 0;

        foreach (var docType in documentTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processedCount++;
            var dtInfo = result.DocumentTypes.First(x => x.Key == docType.Key);

            ReportProgress(progress, result.ConversionId, "Updating document types",
                docType.Name, processedCount, documentTypes.Count);

            try
            {
                var wasModified = false;

                // Handle composition properties first
                if (docType.ContentTypeComposition.Any())
                {
                    wasModified = await UpdateCompositionPropertiesAsync(
                        result, docType, dataTypeMap, isTestRun, updatedCompositionIds, cancellationToken);
                }

                // Handle direct properties
                var directProperties = docType.PropertyTypes
                    .Where(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias))
                    .ToList();

                foreach (var property in directProperties)
                {
                    if (dataTypeMap.TryGetValue(property.DataTypeId, out var targetDataType))
                    {
                        property.DataTypeId = targetDataType.Id;
                        property.DataTypeKey = targetDataType.Key;
                        wasModified = true;
                        dtInfo.PropertiesUpdated++;
                    }
                }

                if (wasModified)
                {
                    using (var scope = _scopeProvider.CreateScope())
                    {
                        _contentTypeService.Save(docType);
                        if (!isTestRun)
                        {
                            scope.Complete();
                        }
                    }

                    dtInfo.Success = true;
                    dtInfo.Message = $"Updated {dtInfo.PropertiesUpdated} properties";

                    await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                        "DocumentType", $"{(isTestRun ? "[DRY RUN] Would save" : "Saved")} document type: {docType.Name}",
                        null, docType.Name, docType.Key.ToString(),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    dtInfo.Skipped = true;
                    dtInfo.Message = "No properties to update";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating document type {DocTypeName}", docType.Name);
                dtInfo.ErrorMessage = ex.Message;

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                    "DocumentType", $"Error updating document type {docType.Name}: {ex.Message}",
                    ex.StackTrace, docType.Name, docType.Key.ToString(),
                    cancellationToken: cancellationToken);
            }
        }
    }

    /// <summary>
    /// Updates properties in composition document types.
    /// Tracks already-updated compositions to prevent duplicate saves when
    /// the same composition is shared across multiple document types.
    /// Each composition save uses its own short-lived scope.
    /// </summary>
    /// <param name="result">The conversion result to populate.</param>
    /// <param name="docType">The document type whose compositions should be checked.</param>
    /// <param name="dataTypeMap">Mapping of source data type ID to target data type.</param>
    /// <param name="isTestRun">Whether this is a test run.</param>
    /// <param name="updatedCompositionIds">Set of already-updated composition IDs to prevent duplicates.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>True if any compositions were modified.</returns>
    protected virtual async Task<bool> UpdateCompositionPropertiesAsync(
        ConversionResult result,
        IContentType docType,
        Dictionary<int, IDataType> dataTypeMap,
        bool isTestRun,
        HashSet<int> updatedCompositionIds,
        CancellationToken cancellationToken)
    {
        var anyModified = false;

        foreach (var composition in docType.ContentTypeComposition)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip if this composition was already updated by another document type
            if (!updatedCompositionIds.Add(composition.Id))
                continue;

            var compositionType = _contentTypeService.Get(composition.Id);
            if (compositionType == null) continue;

            var compositionProperties = compositionType.PropertyTypes
                .Where(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias))
                .ToList();

            var compositionModified = false;

            foreach (var property in compositionProperties)
            {
                if (dataTypeMap.TryGetValue(property.DataTypeId, out var targetDataType))
                {
                    property.DataTypeId = targetDataType.Id;
                    property.DataTypeKey = targetDataType.Key;
                    compositionModified = true;
                }
            }

            if (compositionModified)
            {
                using (var scope = _scopeProvider.CreateScope())
                {
                    _contentTypeService.Save(compositionType);
                    if (!isTestRun)
                    {
                        scope.Complete();
                    }
                }

                anyModified = true;

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                    "DocumentType", $"{(isTestRun ? "[DRY RUN] Would save" : "Saved")} composition: {compositionType.Name}",
                    null, compositionType.Name, compositionType.Key.ToString(),
                    cancellationToken: cancellationToken);
            }
        }

        return anyModified;
    }

    /// <summary>
    /// Converts content node property data from source format to target format.
    ///
    /// Two scans are performed to cover both upgrade paths:
    ///
    /// Scan 1 — Doc types whose editor was just updated in Phase 3:
    ///   Uses the pre-tracked property aliases (captured before Phase 3 changed the editors).
    ///   These properties had the source editor and their values are guaranteed to be in the old format.
    ///
    /// Scan 2 — Doc types already using the target editor (e.g., updated earlier or via uSync):
    ///   The doc type definition is already correct, but stored content values may still be in the
    ///   old format. <see cref="ConvertPropertyValueAsync"/> acts as the gatekeeper: it returns
    ///   <see langword="null"/> when a value is already in the target format, so no unnecessary
    ///   saves are triggered.
    /// </summary>
    /// <param name="result">The conversion result to populate.</param>
    /// <param name="documentTypes">The document types whose content should be converted.</param>
    /// <param name="propertyAliasesToConvert">Pre-tracked mapping of doc type ID to property aliases.</param>
    /// <param name="options">The conversion options (for StopOnError and IsTestRun).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    protected virtual async Task ConvertContentDataAsync(
        ConversionResult result,
        List<IContentType> documentTypes,
        Dictionary<int, HashSet<string>> propertyAliasesToConvert,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        // === Scan 1: Doc types whose editor was just updated in Phase 3 ===
        var processedDocTypeIds = new HashSet<int>();

        foreach (var docType in documentTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processedDocTypeIds.Add(docType.Id);

            if (!propertyAliasesToConvert.TryGetValue(docType.Id, out var aliasesToConvert))
                continue;

            await ConvertContentForDocTypeAsync(
                result, docType, aliasesToConvert, options, progress, cancellationToken);
        }

        // === Scan 2: Doc types already using the target editor ===
        // These were not found in Phase 1 (their editor was already correct), but their content
        // values may still be in the old format. ConvertPropertyValueAsync returns null for values
        // already in the target format, so only genuinely unconverted content is saved.
        await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
            "Conversion", "Phase 4b: Scanning content with target editor for unconverted values", null,
            cancellationToken: cancellationToken);

        var allDocTypes = _contentTypeService.GetAll();

        foreach (var docType in allDocTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (processedDocTypeIds.Contains(docType.Id))
                continue;

            var targetAliases = docType.PropertyTypes
                .Concat(docType.CompositionPropertyTypes)
                .Where(pt => pt.PropertyEditorAlias == TargetPropertyEditorAlias)
                .Select(pt => pt.Alias)
                .ToHashSet();

            if (targetAliases.Count == 0)
                continue;

            await ConvertContentForDocTypeAsync(
                result, docType, targetAliases, options, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Converts content property values for all content nodes of a given document type.
    /// Only properties whose aliases are in <paramref name="aliasesToConvert"/> are processed.
    /// A property is only saved when <see cref="ConvertPropertyValueAsync"/> returns a non-null value,
    /// ensuring the method is safe to call on doc types that may already be partially converted.
    /// For culture-variant properties, each culture is converted independently.
    /// Each content save uses its own short-lived scope.
    /// </summary>
    /// <param name="result">The conversion result to populate.</param>
    /// <param name="docType">The document type whose content nodes should be processed.</param>
    /// <param name="aliasesToConvert">The property aliases to check and convert.</param>
    /// <param name="options">The conversion options (for StopOnError and IsTestRun).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    protected virtual async Task ConvertContentForDocTypeAsync(
        ConversionResult result,
        IContentType docType,
        HashSet<string> aliasesToConvert,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var contentNodes = _contentService.GetPagedOfType(
            docType.Id, 0, int.MaxValue, out long totalRecords, null!);

        var processedCount = 0;

        foreach (var content in contentNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            processedCount++;
            ReportProgress(progress, result.ConversionId, "Converting content",
                content.Name ?? $"Content {content.Id}", processedCount, (int)totalRecords);

            var contentInfo = new ContentConversionInfo
            {
                Id = content.Id,
                Key = content.Key,
                Name = content.Name ?? string.Empty
            };

            try
            {
                var wasModified = false;

                var properties = content.Properties
                    .Where(p => aliasesToConvert.Contains(p.Alias))
                    .ToList();

                foreach (var property in properties)
                {
                    // For culture-variant properties, process each culture value independently.
                    // For invariant properties, use culture = null (the single invariant value).
                    IEnumerable<string?> cultures = property.PropertyType.Variations.HasFlag(ContentVariation.Culture)
                        ? property.Values.Select(v => v.Culture).Where(c => c != null).Distinct()
                        : new string?[] { null };

                    foreach (var culture in cultures)
                    {
                        var oldValue = property.GetValue(culture);
                        if (oldValue == null) continue;

                        var newValue = await ConvertPropertyValueAsync(oldValue, property);
                        if (newValue != null)
                        {
                            property.SetValue(newValue, culture);
                            wasModified = true;
                            contentInfo.PropertiesConverted++;
                        }
                    }
                }

                if (wasModified)
                {
                    if (!options.IsTestRun)
                    {
                        if (options.PublishAfterConversion)
                        {
                            _contentService.SaveAndPublish(content);
                        }
                        else
                        {
                            _contentService.Save(content);
                        }
                    }

                    contentInfo.Success = true;
                    contentInfo.Message = $"{(options.IsTestRun ? "[DRY RUN] Would convert" : "Converted")} {contentInfo.PropertiesConverted} properties";

                    await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                        "Content", $"{(options.IsTestRun ? "[DRY RUN] Would convert" : "Converted")} {contentInfo.PropertiesConverted} " +
                            $"propert{(contentInfo.PropertiesConverted == 1 ? "y" : "ies")} on content '{content.Name}'",
                        null, content.Name, content.Key.ToString(),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    contentInfo.Skipped = true;
                    contentInfo.Message = "No properties to convert";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting content {ContentName} (ID: {ContentId})",
                    content.Name, content.Id);
                contentInfo.ErrorMessage = ex.Message;

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                    "Content", $"Error converting content {content.Name}: {ex.Message}",
                    ex.StackTrace, content.Name, content.Key.ToString(),
                    cancellationToken: cancellationToken);

                if (options.StopOnError)
                {
                    result.ContentNodes.Add(contentInfo);
                    throw;
                }
            }

            result.ContentNodes.Add(contentInfo);
        }
    }

    /// <summary>
    /// Creates a target data type based on the source data type configuration.
    /// Must be implemented by derived classes to provide converter-specific logic.
    /// </summary>
    /// <param name="sourceDataType">The source data type to convert from.</param>
    /// <returns>The new target data type, or null if creation is not possible.</returns>
    protected abstract Task<IDataType?> CreateTargetDataTypeAsync(IDataType sourceDataType);

    /// <summary>
    /// Converts a property value from source format to target format.
    /// Must be implemented by derived classes to provide converter-specific logic.
    /// </summary>
    /// <param name="sourceValue">The source property value to convert.</param>
    /// <param name="property">The property being converted, for context.</param>
    /// <returns>The converted value, or null if conversion is not possible.</returns>
    protected abstract Task<object?> ConvertPropertyValueAsync(object sourceValue, IProperty property);

    /// <summary>
    /// Builds a mapping of document type ID to the set of property aliases that need conversion.
    /// This must be called BEFORE Phase 3 modifies the property definitions.
    /// </summary>
    /// <param name="documentTypes">The document types to scan.</param>
    /// <returns>Mapping of document type ID to property aliases.</returns>
    private Dictionary<int, HashSet<string>> TrackPropertyAliases(List<IContentType> documentTypes)
    {
        var propertyAliasesToConvert = new Dictionary<int, HashSet<string>>();

        foreach (var docType in documentTypes)
        {
            var aliasSet = new HashSet<string>();

            foreach (var prop in docType.PropertyTypes
                .Where(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias)))
            {
                aliasSet.Add(prop.Alias);
            }

            foreach (var prop in docType.CompositionPropertyTypes
                .Where(pt => SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias)))
            {
                aliasSet.Add(prop.Alias);
            }

            if (aliasSet.Count > 0)
            {
                propertyAliasesToConvert[docType.Id] = aliasSet;
                _logger.LogInformation("Tracked {Count} properties for DocType {DocType}: {Aliases}",
                    aliasSet.Count, docType.Name, string.Join(", ", aliasSet));
            }
        }

        return propertyAliasesToConvert;
    }

    /// <summary>
    /// Reports progress to the optional progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter, or null.</param>
    /// <param name="conversionId">The conversion ID.</param>
    /// <param name="phase">The current phase description.</param>
    /// <param name="currentItem">The current item being processed.</param>
    /// <param name="processedCount">Number of items processed so far.</param>
    /// <param name="totalCount">Total number of items to process.</param>
    private static void ReportProgress(
        IProgress<ConversionProgress>? progress,
        Guid conversionId,
        string phase,
        string currentItem,
        int processedCount,
        int totalCount)
    {
        progress?.Report(new ConversionProgress
        {
            ConversionId = conversionId,
            Phase = phase,
            CurrentItem = currentItem,
            ProcessedCount = processedCount,
            TotalCount = totalCount,
            Status = ConversionStatus.Running
        });
    }
}
