namespace Umbraco.Community.LegacyFeatureConverter.Converters;

/// <summary>
/// Shared contract for every converter family surfaced in the Legacy Feature Converter
/// backoffice UI (property converters, macro converters, …).
///
/// Carries only the family-agnostic metadata used by the converter picker card UI plus a
/// single cost-preview method <see cref="GetAffectedUnitCountAsync"/> whose meaning each
/// family defines (e.g. <see cref="IPropertyConverter"/> returns the number of affected
/// document types; a future <c>IMacroConverter</c> would return the number of macros used
/// in content).
///
/// Execution signatures live on the specialized sub-interfaces so each family can model
/// its inputs naturally (a property converter selects document types, a macro converter
/// selects macros).
/// </summary>
public interface ILegacyFeatureConverter
{
    /// <summary>
    /// Gets the human-readable name of this converter (e.g., "Nested Content to Block List").
    /// </summary>
    string ConverterName { get; }

    /// <summary>
    /// Gets a brief description of what this converter does, displayed in the backoffice UI.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets a short display name for the converter card UI (e.g., "Nested Content", "Macro").
    /// Defaults to <see cref="ConverterName"/> if not overridden.
    /// </summary>
    string ShortName => ConverterName;

    /// <summary>
    /// Gets the Umbraco backoffice icon for this converter (e.g., "icon-axis-rotation").
    /// Used in the converter picker card UI.
    /// </summary>
    string Icon => "icon-axis-rotation";

    /// <summary>
    /// Gets the category label for this converter (e.g., "Property editor", "Macro").
    /// Used to label converters in the picker card UI and to drive the category filter.
    /// </summary>
    string Category => "Property editor";

    /// <summary>
    /// Gets the count of units this converter would affect, suitable for display on a card
    /// as a "cost preview". Each family defines its own unit (property converters return
    /// affected document types; macro converters return macros used in content).
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The count of affected units.</returns>
    Task<int> GetAffectedUnitCountAsync(CancellationToken cancellationToken = default);
}
