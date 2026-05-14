using System.Globalization;
using Umbraco.Community.LegacyFeatureConverter.Services;
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

namespace Umbraco.Community.LegacyFeatureConverter.Converters.NestedContent;

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
    /// Returns null only for the legitimate "no work to do" cases (empty, null, or already
    /// in Block List format). Real conversion failures throw
    /// <see cref="PropertyConversionException"/> so the outer per-content catch in
    /// <see cref="BasePropertyConverter"/> records an attributed Error row instead of
    /// silently leaving the original NC array sitting against a Block-List-editor property.
    /// </summary>
    /// <param name="sourceValue">The NC JSON array string.</param>
    /// <param name="property">The property being converted, for context.</param>
    /// <returns>The Block List JSON string, or null when there is no work to do.</returns>
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

        // Already in Block List format (JObject). Most of the time there's no work to do, but
        // there's a known legacy data shape we have to repair: an earlier version of this
        // converter (pre-commit 87bb104) ran Newtonsoft with default date parsing, which
        // caused inner property values that looked like ISO dates to be re-serialised with
        // an extra layer of quoting — they end up persisted as `"\"2022-09-09T14:00:00\""`
        // (a JSON-encoded string whose content is itself a quoted string). Umbraco's
        // BlockValue / NestedPropertyIndexValueFactoryBase pipeline then calls
        // `Property.SetValue(propertyValue, …)` with that quoted string against a DateTime
        // property and `TryConvertTo<DateTime>` fails — exactly the indexer error we see.
        //
        // Detect that shape and emit a cleaned-up Block List value so Phase 4b actually
        // saves a repaired version. Re-runs of Thorough will now find these items instead
        // of silently reporting "nothing to convert".
        if (valueString.TrimStart().StartsWith("{"))
        {
            var normalized = NormalizeBlockListJson(valueString, out var fixedCount);
            if (fixedCount > 0)
            {
                _logger.LogInformation(
                    "Repaired {Count} double-encoded inner value(s) on Block List property {PropertyAlias}",
                    fixedCount, property.Alias);
                return Task.FromResult<object?>(normalized);
            }

            _logger.LogDebug("Property {PropertyAlias} value is already in Block List format, skipping", property.Alias);
            return Task.FromResult<object?>(null);
        }

        JArray? jArray;
        try
        {
            jArray = JsonConvert.DeserializeObject<JArray>(valueString, NoDateParsing);
        }
        catch (JsonException ex)
        {
            throw new PropertyConversionException(
                property.Alias, innerKey: null,
                $"Stored value is not valid JSON: {ex.Message}",
                BuildValuePreview(valueString),
                ex);
        }

        if (jArray == null || jArray.Count == 0)
        {
            _logger.LogDebug("No nested content items found for {PropertyAlias}", property.Alias);
            return Task.FromResult<object?>(null);
        }

        _logger.LogInformation(
            "Converting {Count} nested content items for property {PropertyAlias}",
            jArray.Count, property.Alias);

        // Parse JArray to dictionary format (preserves JSON structure in complex values).
        // ParseJArrayToNCValues casts items to JObject; if the stored value is a JSON array
        // of non-objects (mixed array, etc.) we treat it as not-NC and skip rather than
        // crash the converter.
        if (!jArray.All(item => item is JObject))
        {
            _logger.LogDebug("Property {PropertyAlias} contains a non-object JSON array — not Nested Content, skipping",
                property.Alias);
            return Task.FromResult<object?>(null);
        }

        var ncValues = ParseJArrayToNCValues(jArray);

        // Convert NC data to Block List data recursively. May throw
        // PropertyConversionException for nested data corruption — that bubbles up
        // and lands in the outer per-content catch with full attribution.
        var contentData = ConvertNCDataToBLData(ncValues, property.Alias);
        if (contentData == null || contentData.Count == 0)
        {
            // All items dropped. Previously this returned null which the caller treated as
            // "no change" — but Phase 3 (or uSync) has already swapped the editor to Block
            // List, so leaving the original NC array in place is exactly the bug we are
            // fixing. Throw an attributed failure instead.
            throw new PropertyConversionException(
                property.Alias, innerKey: null,
                "Confirmed Nested Content but no items could be converted " +
                "(every referenced element type was missing or skipped).",
                BuildValuePreview(valueString));
        }

        var blockList = BuildBlockListValue(contentData);
        var resultJson = JsonConvert.SerializeObject(blockList);

        // Round-trip validation: catch any shape regression in our output here, in our own
        // code path, instead of relying on Examine's async indexer to surface it later.
        // Umbraco's BlockValue is the same type the indexer's JsonPropertyIndexValueFactoryBase
        // uses, so a clean round-trip here means the indexer will accept the value too.
        if (!IsRoundTripValid(resultJson, out var roundTripError))
        {
            throw new PropertyConversionException(
                property.Alias, innerKey: null,
                $"Produced Block List JSON failed to round-trip as BlockValue: {roundTripError}",
                BuildValuePreview(resultJson));
        }

        return Task.FromResult<object?>(resultJson);
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
    /// Throws <see cref="PropertyConversionException"/> when a confirmed nested NC value
    /// inside an item cannot itself be converted — the alternative (silently keeping the raw
    /// NC array string in place against a Block-List-editor property) is the data-corruption
    /// bug this method must not produce.
    /// </summary>
    /// <param name="ncValues">The nested content items as dictionaries.</param>
    /// <param name="outerPropertyAlias">Alias of the outer property being converted, for exception attribution.</param>
    /// <returns>The converted block list content data, or null if no items could be converted.</returns>
    private List<Dictionary<string, string?>>? ConvertNCDataToBLData(
        IEnumerable<Dictionary<string, string?>> ncValues,
        string outerPropertyAlias)
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

                var outcome = TryConvertNestedNC(
                    value.Value, outerPropertyAlias, value.Key, out var converted, out var failureReason);
                switch (outcome)
                {
                    case NestedNCOutcome.Converted:
                        content[value.Key] = converted;
                        break;
                    case NestedNCOutcome.NotNestedNC:
                        // Legitimate fall-through: value is a textbox, media picker JSON, plain string, etc.
                        // Copy as-is so the block's element-type property keeps its original value.
                        content[value.Key] = value.Value;
                        break;
                    case NestedNCOutcome.ConversionFailed:
                        // Confirmed nested NC that we could not convert. We MUST NOT silently leave
                        // the original NC array string in place — Phase 3 (or uSync) has already
                        // swapped the inner property's editor to Block List, so persisting an NC
                        // array against a Block List editor would feed an unreadable shape to
                        // the indexer (the exact root cause we're closing).
                        throw new PropertyConversionException(
                            outerPropertyAlias, value.Key, failureReason!, BuildValuePreview(value.Value));
                }
            }

            contentData.Add(content);
        }

        return contentData.Count > 0 ? contentData : null;
    }

    /// <summary>
    /// Outcome of attempting to convert a property value that may contain nested NC.
    /// Distinguishes legitimate "leave as-is" cases from real conversion failures so the
    /// caller can keep the value intact for the former and surface an attributed Error
    /// for the latter.
    /// </summary>
    private enum NestedNCOutcome
    {
        /// <summary>Value isn't nested NC (textbox, media picker, plain string, etc.) — caller copies as-is.</summary>
        NotNestedNC,

        /// <summary>Value was nested NC and was successfully converted to a Block List JSON string.</summary>
        Converted,

        /// <summary>Value was confirmed nested NC but conversion failed — caller must throw, NOT fall back to the raw NC string.</summary>
        ConversionFailed,
    }

    /// <summary>
    /// Attempts to detect and recursively convert a property value that may contain nested NC.
    /// Splits the previous null-returning behaviour into three explicit outcomes so the caller
    /// can react correctly: legitimate "not NC" values are copied as-is, but a confirmed NC
    /// value that cannot be converted is signalled as a failure so the caller does NOT
    /// silently leave a raw NC array string against a Block-List-editor property.
    /// </summary>
    /// <param name="value">The property value string to check.</param>
    /// <param name="outerPropertyAlias">Alias of the outer property being converted, for error attribution.</param>
    /// <param name="innerKey">Key of the inner property whose value we're inspecting, for error attribution.</param>
    /// <param name="converted">On <see cref="NestedNCOutcome.Converted"/>, the Block List JSON. Otherwise null.</param>
    /// <param name="failureReason">On <see cref="NestedNCOutcome.ConversionFailed"/>, the human-readable reason. Otherwise null.</param>
    /// <returns>The detected outcome.</returns>
    private NestedNCOutcome TryConvertNestedNC(
        string value,
        string outerPropertyAlias,
        string innerKey,
        out string? converted,
        out string? failureReason)
    {
        converted = null;
        failureReason = null;

        JArray? jArray;
        try
        {
            jArray = JsonConvert.DeserializeObject<JArray>(value, NoDateParsing);
        }
        catch
        {
            // Not valid JSON array — not nested NC
            return NestedNCOutcome.NotNestedNC;
        }

        if (jArray == null)
            return NestedNCOutcome.NotNestedNC;

        // Items must be JObjects for the NC alias check to work. Mixed arrays (strings,
        // numbers, raw values) are not NC — copy as-is.
        if (!jArray.All(item => item is JObject))
            return NestedNCOutcome.NotNestedNC;

        var nestedNCValues = ParseJArrayToNCValues(jArray);

        // The definitive check: is this actually Nested Content?
        // Only NC arrays contain objects with the "ncContentTypeAlias" key.
        // Other JSON arrays (media pickers, empty arrays, etc.) should be copied as-is.
        if (!nestedNCValues.Any(x => x.ContainsKey("ncContentTypeAlias")))
            return NestedNCOutcome.NotNestedNC;

        _logger.LogInformation("Detected nested NC inside '{Outer}' → '{Inner}', converting recursively",
            outerPropertyAlias, innerKey);

        var nestedContentData = ConvertNCDataToBLData(nestedNCValues, outerPropertyAlias);
        if (nestedContentData == null || nestedContentData.Count == 0)
        {
            // Confirmed nested NC (had ncContentTypeAlias) but every item was dropped — most
            // commonly because the referenced element-type aliases are unknown to the
            // current site. Signal a real failure so the caller throws instead of leaving
            // the raw NC array string in place.
            failureReason = "Confirmed nested Nested Content but no items could be converted " +
                            "(every referenced element type was missing or skipped).";
            return NestedNCOutcome.ConversionFailed;
        }

        var nestedBlockList = BuildBlockListValue(nestedContentData);
        var nestedJson = JsonConvert.SerializeObject(nestedBlockList);

        // Round-trip validation: the inner Block List value lives inside the outer block's
        // RawPropertyValues bucket and will eventually be re-deserialized as Umbraco's
        // BlockValue by the Examine indexer (and by the property editor at read time).
        // Catch any shape regressions HERE, with full attribution context.
        if (!IsRoundTripValid(nestedJson, out var roundTripError))
        {
            failureReason = $"Produced inner Block List JSON failed to round-trip as BlockValue: {roundTripError}";
            return NestedNCOutcome.ConversionFailed;
        }

        converted = nestedJson;
        return NestedNCOutcome.Converted;
    }

    /// <summary>
    /// Scans an already-Block-List JSON value for the legacy "double-encoded scalar" shape
    /// produced by the pre-87bb104 converter (Newtonsoft auto-parsing ISO dates to DateTime
    /// then re-serialising them, leaving values like <c>"\"2022-09-09T14:00:00\""</c> in
    /// <see cref="Umbraco.Cms.Core.Models.Blocks.BlockItemData.RawPropertyValues"/>). For
    /// each such value we unwrap one level of JSON-string encoding so the property's actual
    /// runtime value reaches <see cref="Umbraco.Cms.Core.Models.Property.TryConvertAssignedValue"/>
    /// as the underlying date string (which converts cleanly to <see cref="DateTime"/>) rather
    /// than as a quoted string (which throws).
    ///
    /// Returns the repaired JSON when at least one fix was applied; returns <paramref name="json"/>
    /// unchanged when nothing needed fixing. Conservative by design: only touches values that
    /// both start AND end with a literal <c>"</c> character — leaving genuine JSON objects,
    /// arrays, numbers, booleans, and ordinary strings untouched.
    /// </summary>
    private string NormalizeBlockListJson(string json, out int fixedCount)
    {
        fixedCount = 0;

        JObject root;
        try
        {
            // Use NoDateParsing so we don't ALSO eat clean ISO date strings here — we only
            // want to touch values that are actually double-encoded.
            root = JsonConvert.DeserializeObject<JObject>(json, NoDateParsing)
                   ?? new JObject();
        }
        catch (JsonException)
        {
            // Not parseable JSON — leave it alone, the caller's normal flow will throw later.
            return json;
        }

        foreach (var section in new[] { "contentData", "settingsData" })
        {
            if (root[section] is not JArray items) continue;

            foreach (var item in items.OfType<JObject>())
            {
                foreach (var prop in item.Properties().ToList())
                {
                    if (prop.Value.Type != JTokenType.String) continue;

                    var raw = prop.Value.Value<string>();
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') continue;

                    // Looks like a JSON-encoded scalar string. Try one level of unwrap.
                    try
                    {
                        var unwrapped = JsonConvert.DeserializeObject<string>(raw);
                        if (unwrapped != null && !ReferenceEquals(unwrapped, raw) && unwrapped != raw)
                        {
                            prop.Value = new JValue(unwrapped);
                            fixedCount++;
                        }
                    }
                    catch (JsonException)
                    {
                        // Not actually a valid JSON string (e.g. unmatched escapes) — leave it.
                    }
                }
            }
        }

        return fixedCount > 0
            ? JsonConvert.SerializeObject(root)
            : json;
    }

    /// <summary>
    /// Verifies that a Block List JSON string deserializes cleanly into Umbraco's
    /// <see cref="Umbraco.Cms.Core.Models.Blocks.BlockValue"/> — the same type the
    /// Examine indexer uses. Catches shape regressions in our converter output
    /// synchronously, in our own code, instead of leaking them to the async indexer
    /// where the trail is cold.
    /// </summary>
    private static bool IsRoundTripValid(string json, out string? error)
    {
        try
        {
            JsonConvert.DeserializeObject<Umbraco.Cms.Core.Models.Blocks.BlockValue>(json);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Truncates a value to a manageable preview length for inclusion in error messages.
    /// </summary>
    private static string BuildValuePreview(string value)
    {
        const int maxLength = 500;
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
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
