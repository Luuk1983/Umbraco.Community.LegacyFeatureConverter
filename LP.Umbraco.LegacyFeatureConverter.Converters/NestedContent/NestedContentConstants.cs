namespace LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;

/// <summary>
/// Constants for the Nested Content to Block List converter.
/// </summary>
internal static class NestedContentConstants
{
    /// <summary>
    /// Default Nested Content metadata property keys that should be stripped
    /// during conversion (they are NC-specific and not part of the actual content data).
    /// </summary>
    internal static readonly string[] DefaultNCProperties =
    {
        "name",
        "ncContentTypeAlias",
        "PropType",
        "key"
    };
}
