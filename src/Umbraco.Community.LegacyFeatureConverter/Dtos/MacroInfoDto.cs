namespace Umbraco.Community.LegacyFeatureConverter.Dtos;

/// <summary>
/// Information about a single macro shown in the wizard's macro-selection step.
/// Returned by the <c>GetMacros</c> endpoint when the user has picked a macro converter.
/// </summary>
public class MacroInfoDto
{
    /// <summary>The stable key used in <c>SelectedMacroKeys</c> when submitting the conversion.</summary>
    public Guid Key { get; set; }

    /// <summary>The macro alias as it appears in content markup.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>The macro's human-readable name (falls back to alias for orphans).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional backoffice icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Number of content nodes that reference this macro.</summary>
    public int UsageCount { get; set; }

    /// <summary>Number of rich-text property values containing this macro.</summary>
    public int RtePropertyCount { get; set; }
}
