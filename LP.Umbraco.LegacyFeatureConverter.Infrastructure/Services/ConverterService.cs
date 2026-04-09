using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;

namespace LP.Umbraco.LegacyFeatureConverter.Infrastructure.Services;

/// <summary>
/// Service for discovering and managing property converters.
/// Converters are automatically discovered via dependency injection —
/// any <see cref="IPropertyConverter"/> registered in the DI container is available here.
/// </summary>
public class ConverterService : IConverterService
{
    private readonly IEnumerable<IPropertyConverter> _converters;
    private readonly IContentTypeService _contentTypeService;
    private readonly ILogger<ConverterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConverterService"/> class.
    /// </summary>
    /// <param name="converters">All registered property converters, injected via DI.</param>
    /// <param name="contentTypeService">The Umbraco content type service.</param>
    /// <param name="logger">The logger instance.</param>
    public ConverterService(
        IEnumerable<IPropertyConverter> converters,
        IContentTypeService contentTypeService,
        ILogger<ConverterService> logger)
    {
        _converters = converters ?? throw new ArgumentNullException(nameof(converters));
        _contentTypeService = contentTypeService ?? throw new ArgumentNullException(nameof(contentTypeService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IEnumerable<IPropertyConverter> GetAllConverters()
    {
        return _converters;
    }

    /// <inheritdoc />
    public IPropertyConverter? GetConverterByName(string converterName)
    {
        return _converters.FirstOrDefault(c =>
            c.ConverterName.Equals(converterName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConverterMetadata>> GetConverterMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        var metadata = new List<ConverterMetadata>();

        foreach (var converter in _converters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var count = await converter.GetAffectedDocumentTypesCountAsync(
                    cancellationToken: cancellationToken);

                metadata.Add(new ConverterMetadata
                {
                    Name = converter.ConverterName,
                    Description = converter.Description,
                    ShortName = converter.ShortName,
                    Icon = converter.Icon,
                    Category = converter.Category,
                    SourceAliases = converter.SourcePropertyEditorAliases,
                    TargetAlias = converter.TargetPropertyEditorAlias,
                    AffectedDocumentTypesCount = count
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metadata for converter {ConverterName}",
                    converter.ConverterName);
            }
        }

        return metadata;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentTypeInfo>> GetAffectedDocumentTypesAsync(
        string converterName,
        Guid[]? selectedKeys = null,
        CancellationToken cancellationToken = default)
    {
        var converter = GetConverterByName(converterName);
        if (converter == null)
        {
            _logger.LogWarning("Converter {ConverterName} not found", converterName);
            return Enumerable.Empty<DocumentTypeInfo>();
        }

        var allDocumentTypes = _contentTypeService.GetAll().ToList();

        // Filter by selection if provided
        if (selectedKeys is { Length: > 0 })
        {
            allDocumentTypes = allDocumentTypes
                .Where(dt => selectedKeys.Contains(dt.Key))
                .ToList();
        }

        var documentTypeInfos = new List<DocumentTypeInfo>();

        foreach (var docType in allDocumentTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var propertyCount = 0;

            // Check direct properties
            var directProps = docType.PropertyTypes
                .Where(pt => converter.SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias))
                .ToList();

            propertyCount += directProps.Count;

            // Check composition properties
            var compositionProps = docType.CompositionPropertyTypes
                .Where(pt => converter.SourcePropertyEditorAliases.Contains(pt.PropertyEditorAlias))
                .ToList();

            propertyCount += compositionProps.Count;

            if (propertyCount > 0)
            {
                documentTypeInfos.Add(new DocumentTypeInfo
                {
                    Key = docType.Key,
                    Name = docType.Name ?? docType.Alias,
                    Alias = docType.Alias,
                    Icon = docType.Icon,
                    PropertiesCount = propertyCount
                });
            }
        }

        return await Task.FromResult(documentTypeInfos.OrderBy(dt => dt.Name));
    }

    /// <inheritdoc />
    public async Task<ConversionPlan> ComputePlanAsync(
        string converterName,
        ConversionApproach approach,
        CancellationToken cancellationToken = default)
    {
        var converter = GetConverterByName(converterName);
        if (converter == null)
        {
            _logger.LogWarning("Converter {ConverterName} not found", converterName);
            return new ConversionPlan { Approach = approach, ComputedAt = DateTime.UtcNow };
        }

        return await converter.ComputePlanAsync(approach, cancellationToken);
    }
}
