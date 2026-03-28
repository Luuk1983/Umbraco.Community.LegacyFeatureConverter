using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Extensions;

namespace LP.Umbraco.LegacyFeatureConverter.Hubs;

/// <summary>
/// Registers the SignalR hub endpoint for the Legacy Feature Converter.
/// Implements <see cref="IAreaRoutes"/> for automatic discovery by Umbraco's routing system.
/// </summary>
public class ConversionHubRoutes : IAreaRoutes
{
    private readonly IRuntimeState _runtimeState;
    private readonly string _umbracoPathSegment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionHubRoutes"/> class.
    /// </summary>
    /// <param name="globalSettings">Umbraco global settings containing the backoffice path.</param>
    /// <param name="hostingEnvironment">The Umbraco hosting environment.</param>
    /// <param name="runtimeState">The current runtime state.</param>
    public ConversionHubRoutes(
        IOptions<GlobalSettings> globalSettings,
        IHostingEnvironment hostingEnvironment,
        IRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
        _umbracoPathSegment = globalSettings.Value.GetUmbracoMvcArea(hostingEnvironment);
    }

    /// <summary>
    /// Creates the SignalR hub route. Only maps when Umbraco is fully running.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    public void CreateRoutes(IEndpointRouteBuilder endpoints)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
            return;

        endpoints.MapHub<ConversionHub>(
            $"/{_umbracoPathSegment}/LegacyFeatureConverter/ConversionHub");
    }
}
