using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

/// <summary>
/// In-flight state shared across the phases of a single macro conversion run.
/// Lets <see cref="MacroToRichTextBlockConverter"/> remember "for macro X, the element type
/// I created in Phase 2 is Y" when Phase 5 rewrites content.
/// </summary>
internal sealed class MacroConversionContext
{
    /// <summary>
    /// Maps macro alias → the element type used for that macro (existing or created this run).
    /// </summary>
    public Dictionary<string, IContentType> ElementTypesByMacroAlias { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Macro aliases for which Phase 2 created a new element type during this run (as opposed
    /// to reusing an existing one). Kept for audit/logging purposes — Phase 3 always runs
    /// for every macro so an interrupted previous run can self-correct on the next attempt.
    /// </summary>
    public HashSet<string> ElementTypesCreatedThisRun { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Partial view filenames that the converter has already created during this run.
    /// Used to avoid double-writing when several content nodes reference the same macro.
    /// </summary>
    public HashSet<string> PartialsWritten { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Data type IDs whose Rich Text block configuration has already been updated this run.
    /// Phase 3 batches one save per data type.
    /// </summary>
    public HashSet<int> ConfiguredDataTypeIds { get; } = new();
}
