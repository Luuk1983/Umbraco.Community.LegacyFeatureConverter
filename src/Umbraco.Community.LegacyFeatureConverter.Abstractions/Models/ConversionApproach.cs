namespace Umbraco.Community.LegacyFeatureConverter.Models;

/// <summary>
/// Determines how thoroughly the converter discovers what to process.
/// The two values mean different things per converter family, but share the principle
/// "Fast does the primary scope; Thorough adds a recovery pass for drift."
/// </summary>
public enum ConversionApproach
{
    /// <summary>
    /// The primary, metadata-driven scope:
    /// <list type="bullet">
    ///   <item>Property converters: scan document types for properties using the source property editor;
    ///         update those document types, their data types, and all associated content.</item>
    ///   <item>Macro converters: process every macro currently registered in Umbraco; create element types,
    ///         configure macro-enabled RTE data types, write stub partial views, and rewrite content
    ///         containing markup for the registered macros.</item>
    /// </list>
    /// </summary>
    Fast,

    /// <summary>
    /// Everything <see cref="Fast"/> does, plus a recovery pass over content that's drifted from
    /// the metadata:
    /// <list type="bullet">
    ///   <item>Property converters: also scan content of document types already using the target
    ///         property editor, catching the uSync scenario (schema migrated but content values
    ///         still in the old format).</item>
    ///   <item>Macro converters: also scan content for orphan macro markup — aliases whose
    ///         <c>IMacro</c> definition no longer exists. For each orphan, if an element type
    ///         matching the alias still exists in Umbraco (from a prior conversion), rewrite the
    ///         markup against it. If not, log a warning and leave it alone.</item>
    /// </list>
    /// Slower due to the extra content scan.
    /// </summary>
    Thorough
}
