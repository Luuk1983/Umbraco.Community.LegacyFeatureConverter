using Newtonsoft.Json;

namespace LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;

/// <summary>
/// Represents the JSON structure of a Block List property value.
/// Uses Newtonsoft.Json for serialization because Umbraco 13's block editors
/// use Newtonsoft internally for deserialization.
/// </summary>
internal class BlockListValue
{
    /// <summary>
    /// Gets or sets the layout information (content and settings UDI references).
    /// </summary>
    [JsonProperty("layout")]
    public BlockListLayout Layout { get; set; } = null!;

    /// <summary>
    /// Gets or sets the content data items.
    /// </summary>
    [JsonProperty("contentData")]
    public List<Dictionary<string, string?>> ContentData { get; set; } = new();

    /// <summary>
    /// Gets or sets the settings data items (empty for NC conversions).
    /// </summary>
    [JsonProperty("settingsData")]
    public List<Dictionary<string, string?>> SettingsData { get; set; } = new();
}

/// <summary>
/// Represents the layout section of a Block List value.
/// The JSON property name must match the Block List editor alias exactly.
/// </summary>
internal class BlockListLayout
{
    /// <summary>
    /// Gets or sets the content UDI references that define the block order.
    /// The property name "Umbraco.BlockList" is required by the Block List editor.
    /// </summary>
    [JsonProperty("Umbraco.BlockList")]
    public List<Dictionary<string, string>> ContentUdi { get; set; } = new();

    /// <summary>
    /// Initializes a new instance with the specified content and settings UDI lists.
    /// </summary>
    /// <param name="contentUdis">The content UDI references.</param>
    /// <param name="settingsUdis">The settings UDI references (typically empty for NC conversion).</param>
    public BlockListLayout(
        List<Dictionary<string, string>> contentUdis,
        List<Dictionary<string, string>> settingsUdis)
    {
        ContentUdi = contentUdis;
    }
}
