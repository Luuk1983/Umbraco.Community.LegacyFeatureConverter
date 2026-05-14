using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Aggregated result of scanning Umbraco content for macro usage. Produced by
/// <see cref="Services.IMacroConverterQueryService"/> and consumed by the macro converter
/// and the controller endpoint that powers the wizard's macro-selection step.
/// </summary>
public class MacroScanResult
{
    /// <summary>One entry per macro currently registered in Umbraco that the scan considered.</summary>
    public List<MacroAliasUsage> AliasUsage { get; set; } = new();

    /// <summary>
    /// The distinct content node IDs that referenced any registered macro during the scan.
    /// Drives Phase 5's Fast-mode rewrite loop.
    /// </summary>
    public List<int> AffectedContentIds { get; set; } = new();

    /// <summary>
    /// Distinct macro aliases encountered in content whose <c>IMacro</c> definition no longer
    /// exists in Umbraco. Populated alongside the rest of the scan — the converter chooses
    /// whether to act on them (Thorough rewrites against an existing element type when one
    /// matches the alias; Fast logs a heads-up and skips).
    /// </summary>
    public List<OrphanMacroOccurrence> Orphans { get; set; } = new();
}

/// <summary>
/// Usage statistics for a single macro known to Umbraco's <see cref="Cms.Core.Services.IMacroService"/>.
///
/// The scan is driven by the IMacro registry. Macro markup found in content whose alias has no
/// corresponding <c>IMacro</c> definition is collected separately on
/// <see cref="MacroScanResult.Orphans"/>.
/// </summary>
public class MacroAliasUsage
{
    /// <summary>The macro alias.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The resolved <see cref="IMacro"/> definition (never null in the IMacro-driven flow).</summary>
    public IMacro Macro { get; set; } = null!;

    /// <summary>
    /// Number of distinct content nodes that contain at least one usage of this macro
    /// inside an RTE whose data type has <c>umbmacro</c> in its toolbar.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Number of distinct rich-text property values (content × property × culture) containing
    /// this macro across the whole scan, including nested RTE properties inside blocks.
    /// </summary>
    public int RtePropertyCount { get; set; }
}

/// <summary>
/// One distinct content reference to a macro alias that has no <c>IMacro</c> definition.
/// The Thorough macro approach can rewrite these against an existing element type whose alias
/// matches; Fast does not touch them but surfaces an info-level summary in the conversion log.
/// </summary>
public class OrphanMacroOccurrence
{
    /// <summary>The orphan macro alias as it appears in markup.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Content node ID where the orphan markup lives.</summary>
    public int ContentId { get; set; }

    /// <summary>The property alias (may include nested-block hop notation like <c>blocks&gt;richContent</c>).</summary>
    public string PropertyAlias { get; set; } = string.Empty;

    /// <summary>The culture of the value containing the orphan markup, or null for invariant.</summary>
    public string? Culture { get; set; }
}
