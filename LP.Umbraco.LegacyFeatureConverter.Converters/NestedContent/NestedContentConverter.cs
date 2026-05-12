using System.Globalization;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using static Umbraco.Cms.Core.Constants;
using static Umbraco.Cms.Core.PropertyEditors.BlockListConfiguration;
using DataType = Umbraco.Cms.Core.Models.DataType;

namespace LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;

/// <summary>
/// Converts Nested Content properties to Block List properties.
/// Preserves all content data and structure, including recursively nested NC properties.
///
/// The converter uses the dictionary-based Block List format for backward compatibility.
/// Umbraco's BlockEditorDataConverter automatically converts this to the modern
/// BlockItemData format when the data is loaded.
/// </summary>
public class NestedContentConverter : BasePropertyConverter
{
    // NC stores property values as raw strings. Disable Newtonsoft's auto date parsing so ISO-formatted
    // date strings stay as JTokenType.String — otherwise they become DateTime tokens that re-serialize
    // with surrounding quotes, producing double-quoted dates in the BlockList output.
    private static readonly JsonSerializerSettings NoDateParsing = new() { DateParseHandling = DateParseHandling.None };

    private readonly IDataValueEditorFactory _dataValueEditorFactory;
    private readonly PropertyEditorCollection _propertyEditorCollection;
    private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="NestedContentConverter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="dataTypeService">The Umbraco data type service.</param>
    /// <param name="contentTypeService">The Umbraco content type service.</param>
    /// <param name="contentService">The Umbraco content service.</param>
    /// <param name="historyService">The conversion history service.</param>
    /// <param name="scopeProvider">The Umbraco scope provider.</param>
    /// <param name="dataValueEditorFactory">Factory for creating data value editors.</param>
    /// <param name="propertyEditorCollection">Collection of available property editors.</param>
    /// <param name="configurationEditorJsonSerializer">Serializer for property editor configuration.</param>
    public NestedContentConverter(
        ILogger<NestedContentConverter> logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider,
        IDataValueEditorFactory dataValueEditorFactory,
        PropertyEditorCollection propertyEditorCollection,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        : base(logger, dataTypeService, contentTypeService, contentService, historyService, scopeProvider)
    {
        _dataValueEditorFactory = dataValueEditorFactory ?? throw new ArgumentNullException(nameof(dataValueEditorFactory));
        _propertyEditorCollection = propertyEditorCollection ?? throw new ArgumentNullException(nameof(propertyEditorCollection));
        _configurationEditorJsonSerializer = configurationEditorJsonSerializer ?? throw new ArgumentNullException(nameof(configurationEditorJsonSerializer));
    }

    /// <inheritdoc />
    public override string ConverterName => "Nested Content to Block List";

    /// <inheritdoc />
    public string ShortName => "Nested Content";

    /// <inheritdoc />
    public override string[] SourcePropertyEditorAliases => new[]
    {
        PropertyEditors.Aliases.NestedContent
    };

    /// <inheritdoc />
    public override string TargetPropertyEditorAlias => PropertyEditors.Aliases.BlockList;

    /// <inheritdoc />
    public override string Description =>
        "Converts Nested Content properties to Block List properties, preserving all content and structure.";

    /// <summary>
    /// Creates a Block List data type based on the Nested Content configuration.
    /// Maps min/max item counts and content type references to block configurations.
    /// </summary>
    /// <param name="sourceDataType">The Nested Content data type to convert from.</param>
    /// <returns>A new Block List data type, or null if the source configuration is invalid.</returns>
    protected override Task<IDataType?> CreateTargetDataTypeAsync(IDataType sourceDataType)
    {
#pragma warning disable CS0618 // NestedContentConfiguration is obsolete — expected for migration from legacy NC
        var ncConfig = sourceDataType.Configuration as NestedContentConfiguration;
#pragma warning restore CS0618

        if (ncConfig == null)
        {
            _logger.LogWarning(
                "Source data type '{DataTypeName}' does not have NestedContentConfiguration",
                sourceDataType.Name);
            return Task.FromResult<IDataType?>(null);
        }

        var blDataType = new DataType(
            new DataEditor(_dataValueEditorFactory),
            _configurationEditorJsonSerializer)
        {
            Editor = _propertyEditorCollection.First(x => x.Alias == PropertyEditors.Aliases.BlockList),
            CreateDate = DateTime.Now,
            Name = $"[Block List] {sourceDataType.Name}",
            Configuration = new BlockListConfiguration
            {
                ValidationLimit = new NumberRange
                {
                    Max = ncConfig.MaxItems == 0 ? null : ncConfig?.MaxItems,
                    Min = ncConfig?.MinItems
                },
            },
        };

        var blConfig = blDataType.Configuration as BlockListConfiguration;
        var blocks = new List<BlockConfiguration>();

        // Map each nested content type to a block configuration
        if (ncConfig?.ContentTypes != null)
        {
            foreach (var ncContentType in ncConfig.ContentTypes)
            {
                if (ncContentType?.Alias == null)
                {
                    _logger.LogWarning("Nested content type has null alias, skipping");
                    continue;
                }

                var elementType = _contentTypeService.Get(ncContentType.Alias);
                if (elementType == null)
                {
                    _logger.LogWarning(
                        "Element type '{Alias}' not found for nested content item",
                        ncContentType.Alias);
                    continue;
                }

                blocks.Add(new BlockConfiguration
                {
                    ContentElementTypeKey = elementType.Key,
                    SettingsElementTypeKey = null, // Nested Content doesn't have settings blocks
                    Label = ncContentType.Template // Copy NC name template as Block List label
                });
            }
        }

        if (blConfig != null)
        {
            blConfig.Blocks = blocks.ToArray();
        }

        return Task.FromResult<IDataType?>(blDataType);
    }

    /// <summary>
    /// Converts a Nested Content property value (JSON array) to Block List format.
    /// Handles recursive nested NC properties within NC items.
    /// </summary>
    /// <param name="sourceValue">The NC JSON array string.</param>
    /// <param name="property">The property being converted, for context.</param>
    /// <returns>The Block List JSON string, or null if conversion fails.</returns>
    protected override Task<object?> ConvertPropertyValueAsync(object sourceValue, IProperty property)
    {
        if (sourceValue is null)
            return Task.FromResult<object?>(null);

        var valueString = sourceValue.ToString();
        if (string.IsNullOrEmpty(valueString))
        {
            _logger.LogDebug("Property value is empty for {PropertyAlias}", property.Alias);
            return Task.FromResult<object?>(null);
        }

        // Already in Block List format (JObject) — no conversion needed
        if (valueString.TrimStart().StartsWith("{"))
        {
            _logger.LogDebug("Property {PropertyAlias} value is already in Block List format, skipping", property.Alias);
            return Task.FromResult<object?>(null);
        }

        try
        {
            var jArray = JsonConvert.DeserializeObject<JArray>(valueString, NoDateParsing);
            if (jArray == null || jArray.Count == 0)
            {
                _logger.LogDebug("No nested content items found for {PropertyAlias}", property.Alias);
                return Task.FromResult<object?>(null);
            }

            _logger.LogInformation(
                "Converting {Count} nested content items for property {PropertyAlias}",
                jArray.Count, property.Alias);

            // Parse JArray to dictionary format (preserves JSON structure in complex values)
            var ncValues = ParseJArrayToNCValues(jArray);

            // Convert NC data to Block List data recursively
            var contentData = ConvertNCDataToBLData(ncValues);
            if (contentData == null || contentData.Count == 0)
            {
                _logger.LogWarning("No content data after conversion for {PropertyAlias}", property.Alias);
                return Task.FromResult<object?>(null);
            }

            var blockList = BuildBlockListValue(contentData);
            var result = JsonConvert.SerializeObject(blockList);

            return Task.FromResult<object?>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert nested content for property {PropertyAlias}", property.Alias);
            return Task.FromResult<object?>(null);
        }
    }

    /// <summary>
    /// Parses a JSON array into a list of dictionaries, preserving JSON structure
    /// in complex property values (objects, arrays) while extracting strings directly.
    /// Null JSON values are stored as null in the dictionary.
    /// </summary>
    /// <param name="jArray">The JSON array to parse.</param>
    /// <returns>A list of dictionaries representing the NC items.</returns>
    internal static List<Dictionary<string, string?>> ParseJArrayToNCValues(JArray jArray)
    {
        return jArray.Select(item =>
        {
            var dict = new Dictionary<string, string?>();
            var jObject = (JObject)item;

            foreach (var prop in jObject.Properties())
            {
                if (prop.Value.Type == JTokenType.Null)
                {
                    dict[prop.Name] = null;
                    continue;
                }

                // NC stores property values as raw strings. String tokens unwrap to their raw value.
                // Date tokens get the ISO 8601 string (NOT ToString(Formatting.None), which would
                // wrap them in quotes — the cause of the double-quoted-date bug). All other tokens
                // (bool, numeric, object, array) keep the canonical JSON form.
                dict[prop.Name] = prop.Value.Type switch
                {
                    JTokenType.String => prop.Value.Value<string>() ?? string.Empty,
                    JTokenType.Date => prop.Value.Value<DateTime>().ToString("o", CultureInfo.InvariantCulture),
                    _ => prop.Value.ToString(Formatting.None)
                };
            }

            return dict;
        }).ToList();
    }

    /// <summary>
    /// Recursively converts nested content data to block list data.
    /// For each NC item:
    ///   1. Resolves the content type via ncContentTypeAlias
    ///   2. Creates a new element UDI
    ///   3. Copies property values, recursively converting any nested NC properties
    ///   4. Non-NC JSON arrays (media pickers, etc.) are copied as-is
    /// </summary>
    /// <param name="ncValues">The nested content items as dictionaries.</param>
    /// <returns>The converted block list content data, or null if no items could be converted.</returns>
    private List<Dictionary<string, string?>>? ConvertNCDataToBLData(
        IEnumerable<Dictionary<string, string?>> ncValues)
    {
        if (ncValues == null || !ncValues.Any())
            return null;

        var contentData = new List<Dictionary<string, string?>>();

        foreach (var ncValue in ncValues)
        {
            var rawContentType = ncValue
                .FirstOrDefault(x => x.Key == "ncContentTypeAlias").Value;

            if (string.IsNullOrEmpty(rawContentType))
            {
                _logger.LogWarning("NC item has no ncContentTypeAlias, skipping");
                continue;
            }

            var contentType = _contentTypeService.Get(rawContentType);
            if (contentType == null)
            {
                _logger.LogWarning(
                    "Content type with alias '{Alias}' was not found. " +
                    "Verify the content type exists and is marked as an Element Type.",
                    rawContentType);
                continue;
            }

            var contentUdi = new GuidUdi("element", Guid.NewGuid()).ToString();

            // Start with the standard Block List content item fields
            var content = new Dictionary<string, string?>
            {
                { "contentTypeKey", contentType.Key.ToString() },
                { "udi", contentUdi },
            };

            // Copy property values, stripping NC metadata fields
            var propertyValues = ncValue
                .Where(x => !NestedContentConstants.DefaultNCProperties.Contains(x.Key));

            foreach (var value in propertyValues)
            {
                if (value.Value == null)
                    continue;

                try
                {
                    // Attempt to detect and recursively convert nested NC
                    var nestedResult = TryConvertNestedNC(value.Value);
                    content[value.Key] = nestedResult ?? value.Value;
                }
                catch (Exception ex)
                {
                    // Not valid JSON or conversion failed — copy value as-is
                    _logger.LogDebug(ex,
                        "Property '{Key}' value could not be parsed as nested NC, copying as-is",
                        value.Key);
                    content[value.Key] = value.Value;
                }
            }

            contentData.Add(content);
        }

        return contentData.Count > 0 ? contentData : null;
    }

    /// <summary>
    /// Attempts to detect and recursively convert a property value that may contain nested NC.
    /// Returns the converted Block List JSON string if the value is nested NC,
    /// or null if it's not nested NC (so the caller can use the original value).
    /// </summary>
    /// <param name="value">The property value string to check.</param>
    /// <returns>Converted Block List JSON if nested NC was detected, otherwise null.</returns>
    private string? TryConvertNestedNC(string value)
    {
        JArray? jArray;
        try
        {
            jArray = JsonConvert.DeserializeObject<JArray>(value, NoDateParsing);
        }
        catch
        {
            // Not valid JSON array — not nested NC
            return null;
        }

        if (jArray == null)
            return null;

        var nestedNCValues = ParseJArrayToNCValues(jArray);

        // The definitive check: is this actually Nested Content?
        // Only NC arrays contain objects with the "ncContentTypeAlias" key.
        // Other JSON arrays (media pickers, empty arrays, etc.) should be copied as-is.
        if (!nestedNCValues.Any(x => x.ContainsKey("ncContentTypeAlias")))
            return null;

        _logger.LogInformation("Detected nested NC, converting recursively");

        var nestedContentData = ConvertNCDataToBLData(nestedNCValues);
        if (nestedContentData == null || nestedContentData.Count == 0)
            return null;

        var nestedBlockList = BuildBlockListValue(nestedContentData);
        return JsonConvert.SerializeObject(nestedBlockList);
    }

    /// <summary>
    /// Builds a complete <see cref="BlockListValue"/> from converted content data,
    /// including the layout section with UDI references.
    /// </summary>
    /// <param name="contentData">The converted content data items.</param>
    /// <returns>A complete Block List value ready for JSON serialization.</returns>
    private static BlockListValue BuildBlockListValue(List<Dictionary<string, string?>> contentData)
    {
        var contentUdiList = new List<Dictionary<string, string>>();
        foreach (var content in contentData)
        {
            if (content.TryGetValue("udi", out var udi) && udi != null)
            {
                contentUdiList.Add(new Dictionary<string, string>
                {
                    { "contentUdi", udi }
                });
            }
        }

        return new BlockListValue
        {
            Layout = new BlockListLayout(contentUdiList, new List<Dictionary<string, string>>()),
            ContentData = contentData,
            SettingsData = new List<Dictionary<string, string?>>()
        };
    }
}
