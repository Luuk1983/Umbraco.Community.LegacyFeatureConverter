using System.Text.RegularExpressions;

namespace Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

/// <summary>
/// Compiled regex patterns used by <see cref="MacroMarkupParser"/> to detect Umbraco macro markup
/// inside rich-text values. Kept internal so consumers don't depend on the patterns directly —
/// extension points should go through <see cref="MacroMarkupParser"/>.
/// </summary>
internal static class MacroRegexes
{
    /// <summary>
    /// Matches a single Umbraco macro element: <c>&lt;?UMBRACO_MACRO ... /&gt;</c>.
    /// Also matches HTML-comment-wrapped variants <c>&lt;!--&lt;?UMBRACO_MACRO ... /&gt;--&gt;</c>
    /// so the comment is captured/removed atomically when replacing.
    /// </summary>
    /// <remarks>
    /// Case-insensitive on the tag name. Dot-matches-all so attribute lines can span newlines.
    /// </remarks>
    public static readonly Regex MacroElement = new(
        @"(?is)(?:<!--\s*)?<\?UMBRACO_MACRO\b[^>]*?/>(?:\s*-->)?",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches a single <c>key="value"</c> or <c>key='value'</c> attribute, capturing
    /// <c>param</c> (the key) and <c>value</c> (the unquoted value).
    /// </summary>
    public static readonly Regex Parameter = new(
        @"(?is)\b(?<param>\w+)\s*=\s*(?:\\)?['""](?<value>[^'""]*)['""]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
