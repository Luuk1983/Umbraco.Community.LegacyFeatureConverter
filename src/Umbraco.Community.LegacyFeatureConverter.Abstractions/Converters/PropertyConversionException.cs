namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Thrown by a property converter when a stored value cannot be converted to a valid
/// target-editor structure. Carries enough attribution context for the outer per-content
/// catch in <see cref="BasePropertyConverter"/> to write a clear, traceable Error row to
/// the conversion history without losing track of the offending property alias or value.
/// </summary>
public class PropertyConversionException : Exception
{
    /// <summary>
    /// The alias of the outer property being converted at the moment of failure.
    /// </summary>
    public string PropertyAlias { get; }

    /// <summary>
    /// When the failure happened inside a nested item (e.g. a property within a block),
    /// the key of the inner property that triggered it. Null when the failure is at the
    /// outer property's level.
    /// </summary>
    public string? InnerKey { get; }

    /// <summary>
    /// Human-readable reason for the failure (e.g. "Element type 'foo' not found").
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// First ~500 chars of the offending value, for inclusion in the log row.
    /// </summary>
    public string ValuePreview { get; }

    public PropertyConversionException(
        string propertyAlias,
        string? innerKey,
        string reason,
        string valuePreview)
        : base(BuildMessage(propertyAlias, innerKey, reason, valuePreview))
    {
        PropertyAlias = propertyAlias;
        InnerKey = innerKey;
        Reason = reason;
        ValuePreview = valuePreview;
    }

    public PropertyConversionException(
        string propertyAlias,
        string? innerKey,
        string reason,
        string valuePreview,
        Exception innerException)
        : base(BuildMessage(propertyAlias, innerKey, reason, valuePreview), innerException)
    {
        PropertyAlias = propertyAlias;
        InnerKey = innerKey;
        Reason = reason;
        ValuePreview = valuePreview;
    }

    private static string BuildMessage(string propertyAlias, string? innerKey, string reason, string valuePreview)
    {
        var location = innerKey is null
            ? $"property '{propertyAlias}'"
            : $"property '{propertyAlias}' → nested key '{innerKey}'";
        return $"Conversion failed for {location}: {reason}. Value preview: {valuePreview}";
    }
}
