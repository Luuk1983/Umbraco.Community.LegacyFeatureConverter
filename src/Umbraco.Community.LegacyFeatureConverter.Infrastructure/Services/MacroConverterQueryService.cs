using Umbraco.Community.LegacyFeatureConverter.Converters.Macros;
using Umbraco.Community.LegacyFeatureConverter.Models;
// RichTextConfigurationInspector lives in Abstractions/Converters/Macros — same namespace as the parser.
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;

/// <summary>
/// Scans Umbraco for macro usage. The IMacro registry is the master source of truth:
/// the scan starts from <see cref="IMacroService.GetAll"/> and only counts content usage in
/// RTE properties whose data type explicitly enables macros (the <c>umbmacro</c> button is
/// present in the toolbar configuration).
///
/// Macro markup whose alias has no corresponding <see cref="IMacro"/> definition (orphan
/// references), and macro markup inside RTE properties whose data type doesn't have
/// <c>umbmacro</c> enabled, are intentionally excluded from the result. The converter treats
/// such drift as a content-side problem the developer must fix at source; metadata (data types,
/// document types, macro registry) is whatever uSync says it is.
///
/// Shared by:
/// <list type="bullet">
///   <item>the macro converter (drives <see cref="MacroAliasUsage"/> for plan + execute phases).</item>
///   <item>the controller endpoint that powers the wizard's macro-selection step.</item>
/// </list>
/// </summary>
public class MacroConverterQueryService : IMacroConverterQueryService
{
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IMacroService _macroService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IJsonSerializer _jsonSerializer;
    private readonly ILogger<MacroConverterQueryService> _logger;

    public MacroConverterQueryService(
        IContentTypeService contentTypeService,
        IContentService contentService,
        IMacroService macroService,
        IDataTypeService dataTypeService,
        IJsonSerializer jsonSerializer,
        ILogger<MacroConverterQueryService> logger)
    {
        _contentTypeService = contentTypeService ?? throw new ArgumentNullException(nameof(contentTypeService));
        _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
        _macroService = macroService ?? throw new ArgumentNullException(nameof(macroService));
        _dataTypeService = dataTypeService ?? throw new ArgumentNullException(nameof(dataTypeService));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<MacroScanResult> ScanForMacroUsageAsync(CancellationToken cancellationToken = default)
    {
        var allMacros = _macroService.GetAll().ToList();
        if (allMacros.Count == 0)
        {
            return Task.FromResult(new MacroScanResult());
        }

        // Master IMacro lookup: alias → IMacro. Every key returned in scan results will be
        // backed by an entry here. Orphan markup is never registered against this dictionary.
        var macrosByAlias = allMacros.ToDictionary(
            m => m.Alias, m => m, StringComparer.OrdinalIgnoreCase);

        // Step 1: find RTE data types where the toolbar configuration includes "umbmacro".
        // These are the only data types the converter will ever modify.
        var macroEnabledRteDataTypeIds = new HashSet<int>();
        foreach (var dt in _dataTypeService.GetByEditorAlias(Constants.PropertyEditors.Aliases.TinyMce))
        {
            if (dt.Configuration is RichTextConfiguration rteConfig && RichTextConfigurationInspector.HasUmbmacroInToolbar(rteConfig))
            {
                macroEnabledRteDataTypeIds.Add(dt.Id);
            }
        }

        // Seed the aggregator with every known IMacro so zero-usage macros still appear in
        // the wizard's picker — the developer might still want to scaffold the element type
        // and partial view for those.
        var aggregator = new MacroAggregator(macrosByAlias);

        // No macro-enabled RTE data types → nothing to scan in content. Return zero-usage
        // entries so the wizard still shows the available macros.
        if (macroEnabledRteDataTypeIds.Count == 0)
        {
            return Task.FromResult(new MacroScanResult
            {
                AliasUsage = aggregator.Build(),
                AffectedContentIds = new List<int>(),
                Orphans = aggregator.BuildOrphans()
            });
        }

        // Step 2: identify the document types that own properties using those RTE data types,
        // plus document types whose block properties might host such RTEs recursively.
        var allDocTypes = _contentTypeService.GetAll().ToList();
        var docTypesToScan = new List<(IContentType DocType, HashSet<string> EligibleRteAliases, HashSet<string> BlockAliases)>();

        foreach (var docType in allDocTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rteAliases = new HashSet<string>();
            var blockAliases = new HashSet<string>();

            foreach (var pt in docType.CompositionPropertyTypes)
            {
                if (IsRichTextEditor(pt.PropertyEditorAlias))
                {
                    if (macroEnabledRteDataTypeIds.Contains(pt.DataTypeId))
                    {
                        rteAliases.Add(pt.Alias);
                    }
                }
                else if (IsBlockEditor(pt.PropertyEditorAlias))
                {
                    blockAliases.Add(pt.Alias);
                }
            }

            if (rteAliases.Count > 0 || blockAliases.Count > 0)
            {
                docTypesToScan.Add((docType, rteAliases, blockAliases));
            }
        }

        // Step 3: walk content of qualifying doc types and aggregate usage per IMacro.
        var affectedContentIds = new HashSet<int>();

        foreach (var (docType, rteAliases, blockAliases) in docTypesToScan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contentNodes = _contentService.GetPagedOfType(
                docType.Id, 0, int.MaxValue, out _, null!);

            foreach (var content in contentNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nodeHasMacro = false;

                foreach (var property in content.Properties)
                {
                    if (rteAliases.Contains(property.Alias))
                    {
                        foreach (var (culture, raw) in EnumeratePropertyValues(property))
                        {
                            if (string.IsNullOrEmpty(raw)) continue;
                            nodeHasMacro |= ScanRichTextValueForMacros(
                                raw!, content, property.Alias, culture, aggregator);
                        }
                    }
                    else if (blockAliases.Contains(property.Alias))
                    {
                        foreach (var (culture, raw) in EnumeratePropertyValues(property))
                        {
                            if (string.IsNullOrEmpty(raw)) continue;
                            nodeHasMacro |= ScanBlockValueForMacros(
                                raw!, content, property.Alias, culture, macroEnabledRteDataTypeIds, aggregator);
                        }
                    }
                }

                if (nodeHasMacro)
                {
                    affectedContentIds.Add(content.Id);
                }
            }
        }

        return Task.FromResult(new MacroScanResult
        {
            AliasUsage = aggregator.Build(),
            AffectedContentIds = affectedContentIds.ToList(),
            Orphans = aggregator.BuildOrphans()
        });
    }

    private static bool IsRichTextEditor(string? alias)
        => string.Equals(alias, Constants.PropertyEditors.Aliases.TinyMce, StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockEditor(string? alias)
        => string.Equals(alias, Constants.PropertyEditors.Aliases.BlockList, StringComparison.OrdinalIgnoreCase)
        || string.Equals(alias, Constants.PropertyEditors.Aliases.BlockGrid, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(string? Culture, string? Raw)> EnumeratePropertyValues(IProperty property)
    {
        if (property.PropertyType.Variations.HasFlag(ContentVariation.Culture))
        {
            foreach (var v in property.Values)
            {
                if (v.Culture == null) continue;
                yield return (v.Culture, property.GetValue(v.Culture)?.ToString());
            }
        }
        else
        {
            yield return (null, property.GetValue()?.ToString());
        }
    }

    /// <summary>
    /// Scans one rich-text property value for macro markup. Known macros are aggregated into
    /// the alias-usage stats; orphans (alias not in the IMacro registry) are accumulated
    /// separately so the Thorough mode can act on them later. Returns true if at least one
    /// <i>known</i> macro was found — Phase 5's Fast loop uses that signal to decide whether
    /// to revisit this content node.
    /// </summary>
    private bool ScanRichTextValueForMacros(
        string rawValue, IContent content, string propertyAlias, string? culture, MacroAggregator aggregator)
    {
        string markup;
        if (RichTextPropertyEditorHelper.TryParseRichTextEditorValue(
                rawValue, _jsonSerializer, _logger, out var rteValue))
        {
            markup = rteValue!.Markup ?? string.Empty;
        }
        else
        {
            markup = rawValue;
        }

        if (!MacroMarkupParser.HasMacro(markup))
        {
            return false;
        }

        var anyKnown = false;
        foreach (var macroString in MacroMarkupParser.GetMacroStrings(markup))
        {
            if (!MacroMarkupParser.TryGetMacroAlias(macroString, out var alias))
            {
                continue;
            }

            if (aggregator.Register(alias, content.Id, propertyAlias, culture))
            {
                anyKnown = true;
            }
            else
            {
                // Orphan — alias has no IMacro definition.
                aggregator.RegisterOrphan(alias, content.Id, propertyAlias, culture);
            }
        }

        return anyKnown;
    }

    /// <summary>
    /// Recursively scans a BlockList/BlockGrid value for RTE properties whose data type allows
    /// macros and which contain macro markup. Block element types whose RTE properties use
    /// non-macro-enabled data types are walked but their RTE values are skipped — same
    /// "metadata is canonical" rule as for top-level properties.
    /// </summary>
    private bool ScanBlockValueForMacros(
        string rawValue,
        IContent content,
        string propertyAlias,
        string? culture,
        HashSet<int> macroEnabledRteDataTypeIds,
        MacroAggregator aggregator)
    {
        JObject root;
        try
        {
            root = JObject.Parse(rawValue);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Block value on content {ContentId} property {PropertyAlias} could not be parsed as JSON; skipping nested-block macro scan.",
                content.Id, propertyAlias);
            return false;
        }

        var contentData = root["contentData"] as JArray;
        if (contentData == null || contentData.Count == 0)
        {
            return false;
        }

        var anyKnown = false;
        foreach (var element in contentData.OfType<JObject>())
        {
            var contentTypeKeyStr = element.Value<string>("contentTypeKey");
            if (!Guid.TryParse(contentTypeKeyStr, out var contentTypeKey)) continue;

            var blockContentType = _contentTypeService.Get(contentTypeKey);
            if (blockContentType == null) continue;

            // Map each declared property on the element's content type to (editor alias, dataTypeId).
            // DistinctBy is defensive: Umbraco's CompositionPropertyTypes can yield the same alias twice
            // when two compositions both inherit (transitively) from the same parent — diamond inheritance.
            // The .Union(PropertyTypes) in Umbraco's impl only dedupes by reference, so the cloned
            // inherited properties slip through. First occurrence wins.
            var byAlias = blockContentType.CompositionPropertyTypes
                .DistinctBy(pt => pt.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    pt => pt.Alias,
                    pt => (EditorAlias: pt.PropertyEditorAlias, DataTypeId: pt.DataTypeId),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var prop in element.Properties())
            {
                if (!byAlias.TryGetValue(prop.Name, out var info)) continue;

                var inner = prop.Value?.ToString();
                if (string.IsNullOrEmpty(inner)) continue;

                if (IsRichTextEditor(info.EditorAlias))
                {
                    // Only scan if the nested RTE property's data type allows macros.
                    if (!macroEnabledRteDataTypeIds.Contains(info.DataTypeId)) continue;
                    anyKnown |= ScanRichTextValueForMacros(
                        inner, content, $"{propertyAlias}>{prop.Name}", culture, aggregator);
                }
                else if (IsBlockEditor(info.EditorAlias))
                {
                    anyKnown |= ScanBlockValueForMacros(
                        inner, content, $"{propertyAlias}>{prop.Name}", culture, macroEnabledRteDataTypeIds, aggregator);
                }
            }
        }

        return anyKnown;
    }

    /// <summary>
    /// Tracks macro usage and orphan occurrences across the scan, then produces the final
    /// <see cref="MacroAliasUsage"/> and <see cref="OrphanMacroOccurrence"/> lists.
    /// Pre-seeded with every known IMacro so zero-usage macros still appear in the output.
    /// </summary>
    private sealed class MacroAggregator
    {
        private readonly IReadOnlyDictionary<string, IMacro> _macrosByAlias;
        private readonly Dictionary<string, AliasState> _byAlias =
            new(StringComparer.OrdinalIgnoreCase);
        // Orphan occurrences: a HashSet of distinct tuples to avoid duplicates when the same
        // alias appears multiple times in the same property value or across cultures.
        private readonly HashSet<(string Alias, int ContentId, string PropertyAlias, string? Culture)> _orphans = new();

        public MacroAggregator(IReadOnlyDictionary<string, IMacro> macrosByAlias)
        {
            _macrosByAlias = macrosByAlias;
            foreach (var kvp in macrosByAlias)
            {
                _byAlias[kvp.Key] = new AliasState
                {
                    CanonicalAlias = kvp.Value.Alias,
                    Macro = kvp.Value
                };
            }
        }

        /// <summary>
        /// Returns true if the alias was registered (known macro), false if the alias has no
        /// <c>IMacro</c> definition (caller should pass it to <see cref="RegisterOrphan"/>).
        /// </summary>
        public bool Register(string alias, int contentId, string propertyAlias, string? culture)
        {
            if (!_macrosByAlias.ContainsKey(alias))
            {
                return false;
            }

            var state = _byAlias[alias];
            state.ContentIds.Add(contentId);
            state.RteProperties.Add((contentId, propertyAlias, culture));
            return true;
        }

        /// <summary>Records one occurrence of an orphan macro alias.</summary>
        public void RegisterOrphan(string alias, int contentId, string propertyAlias, string? culture)
            => _orphans.Add((alias, contentId, propertyAlias, culture));

        public List<MacroAliasUsage> Build()
        {
            return _byAlias.Values
                .Select(s => new MacroAliasUsage
                {
                    Alias = s.CanonicalAlias,
                    Macro = s.Macro,
                    UsageCount = s.ContentIds.Count,
                    RtePropertyCount = s.RteProperties.Count
                })
                .OrderBy(u => u.Alias, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<OrphanMacroOccurrence> BuildOrphans()
        {
            return _orphans
                .Select(o => new OrphanMacroOccurrence
                {
                    Alias = o.Alias,
                    ContentId = o.ContentId,
                    PropertyAlias = o.PropertyAlias,
                    Culture = o.Culture
                })
                .OrderBy(o => o.Alias, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.ContentId)
                .ToList();
        }

        private sealed class AliasState
        {
            public string CanonicalAlias { get; init; } = string.Empty;
            public IMacro Macro { get; init; } = null!;
            public HashSet<int> ContentIds { get; } = new();
            public HashSet<(int ContentId, string PropertyAlias, string? Culture)> RteProperties { get; } = new();
        }
    }
}
