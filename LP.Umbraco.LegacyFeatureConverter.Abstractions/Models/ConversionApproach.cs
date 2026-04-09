namespace LP.Umbraco.LegacyFeatureConverter.Models;

/// <summary>
/// Determines how thoroughly the converter discovers which document types and content nodes to process.
/// </summary>
public enum ConversionApproach
{
    /// <summary>
    /// Scans document types for properties using the source property editor.
    /// Updates those document types, their data types, and all associated content.
    /// Document types with no content are still updated.
    /// Fast — no content value scanning required.
    /// </summary>
    DocumentType,

    /// <summary>
    /// Performs the full DocumentType approach, then additionally scans content nodes
    /// that already use the target property editor to check whether their values were
    /// actually converted. Catches the uSync scenario (document type updated by uSync
    /// but content values still in the old format) and recovers from partial migration failures.
    /// Document types with no content are still updated.
    /// Slower due to the content value scan.
    /// </summary>
    Thorough
}
