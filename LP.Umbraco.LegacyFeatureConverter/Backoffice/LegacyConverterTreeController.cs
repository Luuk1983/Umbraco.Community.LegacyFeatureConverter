using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Web.BackOffice.Trees;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;

namespace LP.Umbraco.LegacyFeatureConverter.Backoffice;

/// <summary>
/// Tree controller for the Legacy Feature Converter section in the Umbraco backoffice.
/// Adds a "Legacy converters" group under Settings with a "Property editors" menu item.
/// </summary>
[PluginController("LegacyFeatureConverter")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
[Tree(
    Constants.Applications.Settings,
    "legacyConverter",
    SortOrder = 20,
    TreeTitle = "Legacy feature converters",
    TreeGroup = "legacyConvertersGroup")]
public class LegacyConverterTreeController : TreeController
{
    private readonly IMenuItemCollectionFactory _menuItemCollectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyConverterTreeController"/> class.
    /// </summary>
    /// <param name="menuItemCollectionFactory">Factory for creating menu item collections.</param>
    /// <param name="localizedTextService">The localized text service.</param>
    /// <param name="umbracoApiControllerTypeCollection">The API controller type collection.</param>
    /// <param name="eventAggregator">The event aggregator.</param>
    public LegacyConverterTreeController(
        IMenuItemCollectionFactory menuItemCollectionFactory,
        ILocalizedTextService localizedTextService,
        UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
        IEventAggregator eventAggregator)
        : base(localizedTextService, umbracoApiControllerTypeCollection, eventAggregator)
    {
        _menuItemCollectionFactory = menuItemCollectionFactory;
    }

    /// <summary>
    /// Creates the root node for the tree. Clicking it navigates to the overview view.
    /// </summary>
    /// <param name="queryStrings">The query strings from the request.</param>
    /// <returns>The root tree node.</returns>
    protected override ActionResult<TreeNode?> CreateRootNode(FormCollection queryStrings)
    {
        var root = base.CreateRootNode(queryStrings).Value;
        if (root != null)
        {
            root.RoutePath = $"{Constants.Applications.Settings}/legacyConverter/overview";
            root.Icon = "icon-axis-rotation";
            root.HasChildren = false;
            root.MenuUrl = null;
        }

        return root;
    }

    /// <summary>
    /// Gets the tree nodes for this tree. Returns empty since this is a leaf node (no children).
    /// </summary>
    /// <param name="id">The parent node ID.</param>
    /// <param name="queryStrings">The query strings from the request.</param>
    /// <returns>An empty tree node collection.</returns>
    protected override ActionResult<TreeNodeCollection> GetTreeNodes(string id, FormCollection queryStrings)
    {
        return new TreeNodeCollection();
    }

    /// <summary>
    /// Gets the menu items for a tree node. Returns empty since we don't need context menus.
    /// </summary>
    /// <param name="id">The node ID.</param>
    /// <param name="queryStrings">The query strings from the request.</param>
    /// <returns>An empty menu item collection.</returns>
    protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, FormCollection queryStrings)
    {
        return _menuItemCollectionFactory.Create();
    }
}
