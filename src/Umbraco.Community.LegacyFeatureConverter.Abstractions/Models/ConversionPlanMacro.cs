namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Describes a single macro included in a macro conversion plan.
/// Surfaced in the wizard's Impact step so the user can choose which macros to convert.
///
/// The IMacro list registered in Umbraco is the master source. Macro aliases that appear
/// only in content but have no <c>IMacro</c> definition are never included here — the
/// developer must re-register the macro in Umbraco first if they want it converted.
/// </summary>
public class ConversionPlanMacro
{
    /// <summary>The macro's stable key (matches <c>IMacro.Key</c>).</summary>
    public Guid Key { get; set; }

    /// <summary>The macro alias.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The macro's human-readable name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The macro's backoffice icon (optional).</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Number of distinct content nodes that reference this macro inside RTE properties
    /// whose data type allows macros (i.e. has <c>umbmacro</c> in its toolbar). Content in
    /// RTEs that don't allow macros is excluded — those would only be touched if uSync
    /// metadata permitted it, which by definition it doesn't.
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Number of distinct rich-text property values (content × property × culture) containing
    /// this macro across the whole scan, including nested RTE properties inside blocks.
    /// </summary>
    public int RtePropertyCount { get; set; }

    /// <summary>
    /// Gets or sets the alias of the element type the converter will create for this macro.
    /// </summary>
    public string TargetElementTypeAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the target element type already exists.
    /// True means Phase 2 will skip creation and reuse the existing type.
    /// </summary>
    public bool TargetElementTypeExists { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a stub partial view will be created for this macro.
    /// Driven by <see cref="ConversionOptions.GenerateStubPartialViews"/> and whether a partial
    /// at the conventional path already exists.
    /// </summary>
    public bool NeedsPartialView { get; set; }
}
