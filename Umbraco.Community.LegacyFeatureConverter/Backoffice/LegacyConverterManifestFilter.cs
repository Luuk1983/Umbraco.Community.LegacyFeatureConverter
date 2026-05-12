using Umbraco.Cms.Core.Manifest;

namespace Umbraco.Community.LegacyFeatureConverter.Backoffice;

/// <summary>
/// Registers the JavaScript controllers and CSS stylesheets for the
/// Legacy Feature Converter backoffice section.
/// </summary>
public class LegacyConverterManifestFilter : IManifestFilter
{
    /// <summary>
    /// Adds the Legacy Feature Converter package manifest with all required
    /// scripts and stylesheets for the backoffice UI.
    /// </summary>
    /// <param name="manifests">The manifest list to add to.</param>
    public void Filter(List<PackageManifest> manifests)
    {
        manifests.Add(new PackageManifest
        {
            PackageName = "Umbraco.Community.LegacyFeatureConverter",
            Scripts = new[]
            {
                "/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/overview.controller.js",
                "/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/wizard.controller.js",
                "/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/details.controller.js",
            },
            Stylesheets = new[]
            {
                "/App_Plugins/LegacyFeatureConverter/backoffice/legacyConverter/legacyConverter.css",
            }
        });
    }
}
