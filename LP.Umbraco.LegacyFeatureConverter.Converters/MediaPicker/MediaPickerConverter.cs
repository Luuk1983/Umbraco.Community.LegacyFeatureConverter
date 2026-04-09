using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using static Umbraco.Cms.Core.Constants;
using DataType = Umbraco.Cms.Core.Models.DataType;

namespace LP.Umbraco.LegacyFeatureConverter.Converters.MediaPicker;

/// <summary>
/// Converts legacy Media Picker properties (MediaPicker2, MultipleMediaPicker) to MediaPicker3.
/// Supports converting UDI strings (single or comma-separated) to the MediaPicker3 JSON format
/// with media keys, crops, and focal points.
/// </summary>
public class MediaPickerConverter : BasePropertyConverter
{
    private readonly IMediaService _mediaService;
    private readonly IDataValueEditorFactory _dataValueEditorFactory;
    private readonly PropertyEditorCollection _propertyEditorCollection;
    private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaPickerConverter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dataTypeService">The Umbraco data type service.</param>
    /// <param name="contentTypeService">The Umbraco content type service.</param>
    /// <param name="contentService">The Umbraco content service.</param>
    /// <param name="historyService">The conversion history service.</param>
    /// <param name="scopeProvider">The Umbraco scope provider.</param>
    /// <param name="mediaService">The Umbraco media service.</param>
    /// <param name="dataValueEditorFactory">Factory for creating data value editors.</param>
    /// <param name="propertyEditorCollection">Collection of available property editors.</param>
    /// <param name="configurationEditorJsonSerializer">Serializer for property editor configuration.</param>
    public MediaPickerConverter(
        ILogger<MediaPickerConverter> logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider,
        IMediaService mediaService,
        IDataValueEditorFactory dataValueEditorFactory,
        PropertyEditorCollection propertyEditorCollection,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        : base(logger, dataTypeService, contentTypeService, contentService, historyService, scopeProvider)
    {
        _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
        _dataValueEditorFactory = dataValueEditorFactory ?? throw new ArgumentNullException(nameof(dataValueEditorFactory));
        _propertyEditorCollection = propertyEditorCollection ?? throw new ArgumentNullException(nameof(propertyEditorCollection));
        _configurationEditorJsonSerializer = configurationEditorJsonSerializer ?? throw new ArgumentNullException(nameof(configurationEditorJsonSerializer));
    }

    /// <inheritdoc />
    public override string ConverterName => "Legacy Media Picker to MediaPicker3";

    /// <inheritdoc />
    public string ShortName => "Media Picker";

    /// <inheritdoc />
    public string Icon => "icon-picture";

    /// <inheritdoc />
    public override string[] SourcePropertyEditorAliases => new[]
    {
        "Umbraco.MediaPicker2",
        PropertyEditors.Aliases.MultipleMediaPicker
    };

    /// <inheritdoc />
    public override string TargetPropertyEditorAlias => PropertyEditors.Aliases.MediaPicker3;

    /// <inheritdoc />
    public override string Description =>
        "Converts legacy MediaPicker2 and MultipleMediaPicker properties to the modern MediaPicker3 format with support for crops and focal points.";

    /// <summary>
    /// Creates a MediaPicker3 data type with sensible defaults based on the source picker type.
    /// Single pickers get Max=1, multiple pickers get no limit.
    /// </summary>
    /// <param name="sourceDataType">The legacy media picker data type.</param>
    /// <returns>A new MediaPicker3 data type.</returns>
    protected override Task<IDataType?> CreateTargetDataTypeAsync(IDataType sourceDataType)
    {
        var isMultiple = sourceDataType.EditorAlias == PropertyEditors.Aliases.MultipleMediaPicker;

        var mp3DataType = new DataType(
            new DataEditor(_dataValueEditorFactory),
            _configurationEditorJsonSerializer)
        {
            Editor = _propertyEditorCollection.First(x => x.Alias == PropertyEditors.Aliases.MediaPicker3),
            CreateDate = DateTime.Now,
            Name = $"[MediaPicker3] {sourceDataType.Name}",
            Configuration = new MediaPicker3Configuration
            {
                Multiple = isMultiple,
                ValidationLimit = new MediaPicker3Configuration.NumberRange
                {
                    Max = isMultiple ? null : 1,
                    Min = null
                },
                StartNodeId = null,
                EnableLocalFocalPoint = true,
                Crops = Array.Empty<MediaPicker3Configuration.CropConfiguration>(),
                IgnoreUserStartNodes = false,
                Filter = null
            }
        };

        _logger.LogInformation(
            "Created MediaPicker3 data type '{DataTypeName}' (Multiple: {IsMultiple})",
            mp3DataType.Name, isMultiple);

        return Task.FromResult<IDataType?>(mp3DataType);
    }

    /// <summary>
    /// Converts a legacy media picker property value (UDI string or comma-separated UDIs)
    /// to the MediaPicker3 JSON format (array of objects with key, mediaKey, crops, focalPoint).
    /// Values already in MediaPicker3 format (JSON arrays) are returned as-is.
    /// </summary>
    /// <param name="sourceValue">The legacy media picker value (UDI string).</param>
    /// <param name="property">The property being converted.</param>
    /// <returns>The MediaPicker3 JSON array string, or "[]" for empty/invalid values.</returns>
    protected override Task<object?> ConvertPropertyValueAsync(object sourceValue, IProperty property)
    {
        var rawValue = sourceValue?.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            _logger.LogDebug("Property value is empty for {PropertyAlias}", property.Alias);
            return Task.FromResult<object?>("[]");
        }

        try
        {
            // Already in MediaPicker3 format (JSON array) — no conversion needed
            if (rawValue.TrimStart().StartsWith("["))
            {
                _logger.LogDebug(
                    "Property {PropertyAlias} is already in MediaPicker3 format, skipping",
                    property.Alias);
                return Task.FromResult<object?>(null);
            }

            // Parse comma-separated UDI strings
            var udiStrings = rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();

            if (udiStrings.Count == 0)
            {
                _logger.LogDebug("No valid UDIs found for {PropertyAlias}", property.Alias);
                return Task.FromResult<object?>("[]");
            }

            _logger.LogInformation(
                "Converting {Count} media UDI(s) for property {PropertyAlias}",
                udiStrings.Count, property.Alias);

            var mediaItems = new List<MediaPicker3Item>();

            foreach (var udiStr in udiStrings)
            {
                if (!UdiParser.TryParse(udiStr, out Udi? udi) || udi is not GuidUdi guidUdi)
                {
                    _logger.LogWarning(
                        "Failed to parse UDI '{UdiString}' for property {PropertyAlias}",
                        udiStr, property.Alias);
                    continue;
                }

                // Attempt to resolve the media type alias for validation
                string? mediaTypeAlias = null;
                try
                {
                    var media = _mediaService.GetById(guidUdi.Guid);
                    if (media != null)
                    {
                        mediaTypeAlias = media.ContentType.Alias;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Media with key {MediaKey} not found, including anyway",
                            guidUdi.Guid);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Error getting media type for {MediaKey}, including without type alias",
                        guidUdi.Guid);
                }

                mediaItems.Add(new MediaPicker3Item
                {
                    Key = Guid.NewGuid(),
                    MediaKey = guidUdi.Guid,
                    MediaTypeAlias = mediaTypeAlias,
                    Crops = Enumerable.Empty<ImageCropperValue.ImageCropperCrop>(),
                    FocalPoint = new ImageCropperValue.ImageCropperFocalPoint
                    {
                        Left = 0.5m,
                        Top = 0.5m
                    }
                });
            }

            if (mediaItems.Count == 0)
            {
                _logger.LogWarning(
                    "No valid media items after conversion for {PropertyAlias}",
                    property.Alias);
                return Task.FromResult<object?>("[]");
            }

            _logger.LogInformation(
                "Successfully converted {Count} media items for property {PropertyAlias}",
                mediaItems.Count, property.Alias);

            return Task.FromResult<object?>(JsonConvert.SerializeObject(mediaItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to convert media picker value for property {PropertyAlias}",
                property.Alias);
            return Task.FromResult<object?>("[]");
        }
    }

    /// <summary>
    /// Represents a single item in the MediaPicker3 JSON format.
    /// Mirrors Umbraco's internal MediaWithCropsDto structure.
    /// </summary>
    private class MediaPicker3Item
    {
        /// <summary>
        /// Unique key for this picker item instance.
        /// </summary>
        [JsonProperty("key")]
        public Guid Key { get; set; }

        /// <summary>
        /// The GUID key of the media item in the media library.
        /// </summary>
        [JsonProperty("mediaKey")]
        public Guid MediaKey { get; set; }

        /// <summary>
        /// The alias of the media type (e.g., "Image", "File").
        /// </summary>
        [JsonProperty("mediaTypeAlias")]
        public string? MediaTypeAlias { get; set; }

        /// <summary>
        /// The crop definitions for this media item.
        /// </summary>
        [JsonProperty("crops")]
        public IEnumerable<ImageCropperValue.ImageCropperCrop>? Crops { get; set; }

        /// <summary>
        /// The focal point for this media item (default: center 0.5, 0.5).
        /// </summary>
        [JsonProperty("focalPoint")]
        public ImageCropperValue.ImageCropperFocalPoint? FocalPoint { get; set; }
    }
}
