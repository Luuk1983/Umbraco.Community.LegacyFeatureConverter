using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.PropertyEditors;

namespace Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

/// <summary>
/// Pure inspection helpers for <see cref="RichTextConfiguration"/>. Lives in Abstractions so
/// both the converter (Converters project) and the query service (Infrastructure project)
/// can share the same JSON-parsing logic without duplication.
/// </summary>
public static class RichTextConfigurationInspector
{
    private const string UmbmacroToolbarCommand = "umbmacro";

    /// <summary>
    /// Returns true when the RTE data type's <c>toolbar</c> configuration includes the
    /// <c>umbmacro</c> command (i.e. the developer enabled the "Insert macro" toolbar button).
    ///
    /// Umbraco persists the toolbar as a JSON array on the prevalue editor's <c>Editor</c>
    /// object. After Newtonsoft deserialization the <c>Editor</c> field of
    /// <see cref="RichTextConfiguration"/> lands as a <c>JObject</c>; in rarer scenarios it
    /// can be a <c>Dictionary&lt;string, object&gt;</c> or a <c>System.Text.Json</c> shape.
    /// This implementation normalizes via a JSON round-trip when the shape isn't already a
    /// <c>JObject</c>, so it's robust against any of those.
    /// </summary>
    public static bool HasUmbmacroInToolbar(RichTextConfiguration config)
    {
        if (config.Editor is null) return false;

        JObject editorObj;
        if (config.Editor is JObject jObject)
        {
            editorObj = jObject;
        }
        else
        {
            try
            {
                var json = JsonConvert.SerializeObject(config.Editor);
                editorObj = JObject.Parse(json);
            }
            catch
            {
                return false;
            }
        }

        if (editorObj["toolbar"] is not JArray toolbar) return false;

        return toolbar.Any(t =>
            string.Equals(t?.Value<string>(), UmbmacroToolbarCommand, StringComparison.OrdinalIgnoreCase));
    }
}
