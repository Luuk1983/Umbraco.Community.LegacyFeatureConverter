namespace Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

/// <summary>
/// Pure helper functions for detecting and parsing legacy Umbraco macro markup
/// inside rich-text property values. No dependencies on Umbraco services — safe to call
/// from anywhere (the converter, the query service, tests).
/// </summary>
public static class MacroMarkupParser
{
    /// <summary>
    /// Returns true when <paramref name="content"/> contains at least one
    /// <c>&lt;?UMBRACO_MACRO ... /&gt;</c> element (with or without an HTML comment wrapper).
    /// </summary>
    public static bool HasMacro(string content)
        => !string.IsNullOrEmpty(content) && MacroRegexes.MacroElement.IsMatch(content);

    /// <summary>
    /// Returns every distinct macro element substring found in <paramref name="content"/>,
    /// in document order. Each returned string is the full match (including any
    /// surrounding HTML comment wrapper) so callers can use it directly with
    /// <c>string.Replace</c> for in-place rewriting.
    /// </summary>
    public static string[] GetMacroStrings(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Array.Empty<string>();
        }

        return MacroRegexes.MacroElement
            .Matches(content)
            .Select(m => m.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
    }

    /// <summary>
    /// Extracts every <c>key="value"</c> pair from a single macro markup string.
    /// Recognizes both double- and single-quoted values. Returns an empty dictionary
    /// when no parameters are present.
    /// </summary>
    public static Dictionary<string, string> GetParametersFromMacro(string macroString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(macroString))
        {
            return parameters;
        }

        foreach (System.Text.RegularExpressions.Match match in MacroRegexes.Parameter.Matches(macroString))
        {
            if (!match.Success) continue;
            if (!match.Groups["param"].Success || !match.Groups["value"].Success) continue;

            var key = match.Groups["param"].Value;
            var value = match.Groups["value"].Value;

            // Only the first occurrence of a key wins — defensive against malformed markup.
            parameters.TryAdd(key, value);
        }

        return parameters;
    }

    /// <summary>
    /// Resolves the macro alias from a macro markup string.
    /// Recognizes both <c>macroAlias="…"</c> (the modern attribute name) and the legacy
    /// pre-v8 <c>alias="…"</c> attribute. Returns false when neither is present or the value is empty.
    /// </summary>
    /// <param name="macroString">The macro markup, e.g. <c>&lt;?UMBRACO_MACRO macroAlias="x" /&gt;</c>.</param>
    /// <param name="alias">The extracted alias, or <see cref="string.Empty"/> when not found.</param>
    public static bool TryGetMacroAlias(string macroString, out string alias)
    {
        var parameters = GetParametersFromMacro(macroString);

        if (parameters.TryGetValue("macroAlias", out var modern) && !string.IsNullOrWhiteSpace(modern))
        {
            alias = modern;
            return true;
        }

        if (parameters.TryGetValue("alias", out var legacy) && !string.IsNullOrWhiteSpace(legacy))
        {
            alias = legacy;
            return true;
        }

        alias = string.Empty;
        return false;
    }
}
