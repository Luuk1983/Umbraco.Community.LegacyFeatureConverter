using System.Text;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

/// <summary>
/// Converts legacy Umbraco macros into inline Rich-Text-Editor blocks
/// (<c>&lt;umb-rte-block&gt;</c>) backed by auto-generated element types.
///
/// <para>The flow is metadata-first, mirroring the property converters:</para>
/// <list type="number">
///   <item><b>Phase 1 — Discover.</b> Read the macro registry (<see cref="IMacroService.GetAll"/>) and find Rich Text data types whose toolbar contains <c>umbmacro</c>. Walk content of those data types' properties to count usage.</item>
///   <item><b>Phase 2 — Element types.</b> Create one element type per selected macro. Reuse if one already exists with the same alias; record which were newly created so Phase 3 only touches the ones we own.</item>
///   <item><b>Phase 3 — Configure RTE data types.</b> Add newly-created element types as available blocks on every RTE data type whose toolbar contains <c>umbmacro</c>. Batched: one save per data type.</item>
///   <item><b>Phase 4 — Stub partial views.</b> One stub per selected macro at <c>/Views/Partials/richtext/Components/{MacroName}.cshtml</c>. Never overwrites an existing file.</item>
///   <item><b>Phase 5 — Rewrite content</b> (only when <see cref="ConversionApproach.Thorough"/>). Walks content of qualifying RTE properties and replaces macro markup with <c>&lt;umb-rte-block&gt;</c>. Orphan macro markup (alias has no <c>IMacro</c>) is logged with a warning and left in place — the developer must re-register the macro in Umbraco before re-running if they want it converted.</item>
/// </list>
///
/// <para>Metadata is canonical: data types, document types, and the macro registry on each
/// environment are taken as truth. The converter never tries to "fix" inconsistencies that
/// would just be undone by the next uSync import. Content drift in disallowed RTEs is left
/// alone for the same reason.</para>
/// </summary>
public class MacroToRichTextBlockConverter : BaseMacroConverter
{
    /// <summary>Folder under "Document Types → Element Types" that holds auto-generated macro element types.</summary>
    public static readonly Guid ElementTypeFolderKey = Guid.Parse("2befed8a-d4a0-43fb-ad34-453cb6c2f63d");

    /// <summary>Folder name used when no container exists yet.</summary>
    public const string ElementTypeFolderName = "Converted macros";

    /// <summary>Standard location for the stub partial views generated for converted macros.</summary>
    public const string PartialViewDirectory = "/Views/Partials/richtext/Components/";

    private readonly IJsonSerializer _jsonSerializer;
    private readonly IConfigurationEditorJsonSerializer _configurationEditorJsonSerializer;
    private readonly IMacroConverterQueryService _queryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacroToRichTextBlockConverter"/> class.
    /// </summary>
    public MacroToRichTextBlockConverter(
        ILogger<MacroToRichTextBlockConverter> logger,
        IMacroService macroService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IContentService contentService,
        IFileService fileService,
        IShortStringHelper shortStringHelper,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider,
        IJsonSerializer jsonSerializer,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer,
        IMacroConverterQueryService queryService)
        : base(logger, macroService, contentTypeService, dataTypeService, contentService,
              fileService, shortStringHelper, historyService, scopeProvider)
    {
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _configurationEditorJsonSerializer = configurationEditorJsonSerializer ?? throw new ArgumentNullException(nameof(configurationEditorJsonSerializer));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
    }

    /// <inheritdoc />
    public override string ConverterName => "Macro to Rich Text Block";

    /// <inheritdoc />
    public override string ShortName => "Macro";

    /// <inheritdoc />
    public override string Description =>
        "Replaces inline Umbraco macros inside Rich Text Editor values with element-type-backed blocks.";

    /// <inheritdoc />
    public override string TargetShapeAlias => "richTextBlock";

    /// <summary>
    /// Cheap override of <see cref="ILegacyFeatureConverter.GetAffectedUnitCountAsync"/> —
    /// just counts the macros in the IMacro registry. This is what the converter card shows
    /// as "affected" on the picker. We intentionally do NOT call
    /// <see cref="ComputePlanAsync"/> here: that triggers a full content scan and would make
    /// every backoffice page load that lists converters do a heavy walk through content.
    /// Any failure in that path would also silently drop the macro converter out of the
    /// metadata response. Counting the registry is O(macros), can't fail meaningfully, and
    /// matches the user's mental model ("how many macros are there to potentially convert?").
    /// </summary>
    public override Task<int> GetAffectedUnitCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_macroService.GetAll().Count());
    }

    /// <inheritdoc />
    public override async Task<ConversionPlan> ComputePlanAsync(
        ConversionApproach approach, CancellationToken cancellationToken = default)
    {
        var scan = await _queryService.ScanForMacroUsageAsync(cancellationToken);

        var existingByAlias = _contentTypeService.GetAll()
            .Where(ct => ct.IsElement)
            .ToDictionary(ct => ct.Alias, ct => ct, StringComparer.OrdinalIgnoreCase);

        var planMacros = new List<ConversionPlanMacro>();
        var totalUsages = 0;

        foreach (var usage in scan.AliasUsage)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetAlias = usage.Macro.Alias;
            var elementExists = existingByAlias.ContainsKey(targetAlias);

            planMacros.Add(new ConversionPlanMacro
            {
                Key = usage.Macro.Key,
                Alias = usage.Alias,
                Name = usage.Macro.Name ?? usage.Alias,
                Icon = "icon-settings-alt",
                UsageCount = usage.UsageCount,
                RtePropertyCount = usage.RtePropertyCount,
                TargetElementTypeAlias = targetAlias,
                TargetElementTypeExists = elementExists,
                NeedsPartialView = true
            });

            totalUsages += usage.UsageCount;
        }

        return new ConversionPlan
        {
            Approach = approach,
            ComputedAt = DateTime.UtcNow,
            Macros = planMacros.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            TotalMacroUsages = totalUsages,
            TotalContentNodes = scan.AffectedContentIds.Count
        };
    }

    /// <inheritdoc />
    protected override async Task ExecuteCoreAsync(
        ConversionOptions options,
        ConversionResult result,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ctx = new MacroConversionContext();
        var isThorough = options.Approach == ConversionApproach.Thorough;

        // Phase 1: Discover
        await LogPhaseAsync(result, "Phase 1: Reading macro registry and detecting eligible Rich Text data types", cancellationToken);
        ReportProgress(progress, result.ConversionId, "Reading macro registry", string.Empty, 0, 0);

        var scan = await _queryService.ScanForMacroUsageAsync(cancellationToken);

        // Filter by selection if provided. Macro.Key is always populated (IMacro-driven scan).
        IReadOnlyList<MacroAliasUsage> selected = scan.AliasUsage;
        if (options.SelectedMacroKeys is { Length: > 0 })
        {
            var selectedSet = new HashSet<Guid>(options.SelectedMacroKeys);
            selected = scan.AliasUsage
                .Where(u => selectedSet.Contains(u.Macro.Key))
                .ToList();
        }

        await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
            "Conversion",
            $"Registry has {scan.AliasUsage.Count} macro(s); {selected.Count} selected for conversion. " +
            $"Content scan found {scan.AffectedContentIds.Count} content node(s) referencing registered macros. " +
            $"Approach: {(isThorough ? "Thorough" : "Fast")}.",
            null, cancellationToken: cancellationToken);

        // Phase 2: Ensure element types
        await LogPhaseAsync(result, "Phase 2: Creating element types for selected macros", cancellationToken);

        var phase2Processed = 0;
        foreach (var usage in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            phase2Processed++;
            ReportProgress(progress, result.ConversionId, "Creating element types",
                usage.Alias, phase2Processed, selected.Count);

            await EnsureElementTypeForMacroAsync(usage, options, result, ctx, cancellationToken);
        }

        // Phase 3: Configure RTE data types — only those with umbmacro in toolbar, and only
        // for element types we created in this run (reused ones inherit dev's curation).
        await LogPhaseAsync(result, "Phase 3: Configuring Rich Text data types with the new blocks", cancellationToken);

        await ConfigureRichTextDataTypesAsync(options, result, ctx, cancellationToken);

        // Phase 4: Stub partial views (optional). Always honors IsTestRun.
        if (options.GenerateStubPartialViews && !options.IsTestRun)
        {
            await LogPhaseAsync(result, "Phase 4: Writing stub partial views", cancellationToken);
            foreach (var usage in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteStubPartialViewAsync(usage.Macro, ctx, result, cancellationToken);
            }
        }
        else if (options.IsTestRun && options.GenerateStubPartialViews)
        {
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion", "[DRY RUN] Skipping partial view generation during test run.", null,
                cancellationToken: cancellationToken);
        }

        // Phase 5 — Rewrite content. Runs in BOTH Fast and Thorough mode.
        //
        //   Fast: walks every content node the scan flagged as containing a registered macro
        //         and rewrites that markup into <umb-rte-block> entries.
        //
        //   Thorough: same as Fast, plus an orphan-recovery pass:
        //     - For each orphan alias (markup whose IMacro definition is gone), look up an
        //       element type in Umbraco whose alias matches. If found, treat the orphan as if
        //       it were a known/selected macro — same rewrite path, against the existing element
        //       type. Catches content that was left behind after the IMacro registry was cleaned.
        //     - For orphans whose alias has no matching element type, RewriteContentNodeAsync's
        //       existing per-content warning fires; nothing is rewritten.
        await LogPhaseAsync(result, "Phase 5: Rewriting content values", cancellationToken);

        // Selected aliases drive which markup gets rewritten. We start with the registered
        // macros the user selected, then augment for Thorough orphan recovery below.
        var selectedAliases = new HashSet<string>(
            selected.Select(u => u.Alias), StringComparer.OrdinalIgnoreCase);
        var knownAliases = new HashSet<string>(
            scan.AliasUsage.Select(u => u.Alias), StringComparer.OrdinalIgnoreCase);
        var contentIdsToProcess = new HashSet<int>(scan.AffectedContentIds);

        var orphans = scan.Orphans;

        if (isThorough && orphans.Count > 0)
        {
            // Build alias → existing element type lookup, first-match-wins, case-insensitive.
            var existingElementTypesByAlias = _contentTypeService.GetAll()
                .Where(c => c.IsElement)
                .GroupBy(c => c.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var orphanAlias in orphans.Select(o => o.Alias).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (existingElementTypesByAlias.TryGetValue(orphanAlias, out var elementType))
                {
                    // Recoverable orphan: register the existing element type against this alias
                    // so RewriteRichTextValue treats it like a known macro and writes a block.
                    ctx.ElementTypesByMacroAlias[orphanAlias] = elementType;
                    selectedAliases.Add(orphanAlias);
                    knownAliases.Add(orphanAlias);
                }
                // Unrecoverable orphans stay out of knownAliases on purpose. RewriteContentNodeAsync
                // will see them in markup, won't match knownAliases, and the per-content warning
                // in that method will fire, naming the alias and the property.
            }

            // Ensure we visit every content node that has orphan markup — including ones whose
            // only macro reference is an orphan (those aren't in scan.AffectedContentIds).
            foreach (var orphan in orphans)
            {
                contentIdsToProcess.Add(orphan.ContentId);
            }
        }

        var phase5Processed = 0;
        var phase5Total = contentIdsToProcess.Count;

        foreach (var contentId in contentIdsToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();
            phase5Processed++;

            var content = _contentService.GetById(contentId);
            if (content == null) continue;

            ReportProgress(progress, result.ConversionId, "Rewriting content",
                content.Name ?? $"Content {content.Id}", phase5Processed, phase5Total);

            await RewriteContentNodeAsync(content, selectedAliases, knownAliases, options, result, ctx, cancellationToken);
        }

        // Fast heads-up: tell the dev that running Thorough would have something to do.
        if (!isThorough && orphans.Count > 0)
        {
            var orphanAliases = orphans
                .Select(o => o.Alias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "Conversion",
                $"Heads-up: found {orphanAliases.Count} orphan macro alias(es) in content with no IMacro definition: " +
                $"[{string.Join(", ", orphanAliases)}]. Fast left them untouched. " +
                "Re-run with the Thorough approach to attempt converting them against any element types whose alias matches.",
                null, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Phase 2: Create or reuse the element type for one macro.
    /// Orphan handling is gone — the IMacro-driven scan guarantees <c>usage.Macro</c> is non-null.
    /// </summary>
    private async Task EnsureElementTypeForMacroAsync(
        MacroAliasUsage usage,
        ConversionOptions options,
        ConversionResult result,
        MacroConversionContext ctx,
        CancellationToken ct)
    {
        var alias = usage.Macro.Alias;
        var dtInfo = new DocumentTypeConversionInfo
        {
            Key = usage.Macro.Key,
            Alias = alias,
            Name = usage.Macro.Name ?? alias
        };

        try
        {
            // Reuse existing element type if one already exists with our alias.
            // Phase 3 will still run for reused types — that lets a re-run self-correct after
            // an interrupted previous run. The dedup inside Phase 3 means no save happens
            // when everything is already in sync.
            var existing = _contentTypeService.Get(alias);
            if (existing != null && existing.IsElement)
            {
                ctx.ElementTypesByMacroAlias[usage.Alias] = existing;
                dtInfo.Skipped = true;
                dtInfo.Success = true;
                dtInfo.Message = "Element type already exists";
                result.DocumentTypes.Add(dtInfo);

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                    "DocumentType", $"Element type already exists for macro '{usage.Alias}': {existing.Alias}",
                    null, existing.Name, existing.Key.ToString(), ct);
                return;
            }

            var elementType = BuildElementTypeForMacro(usage.Macro);
            if (!options.IsTestRun)
            {
                using var scope = _scopeProvider.CreateScope();
                _contentTypeService.Save(elementType);
                scope.Complete();
            }
            ctx.ElementTypesByMacroAlias[usage.Alias] = elementType;
            ctx.ElementTypesCreatedThisRun.Add(usage.Alias);

            dtInfo.Success = true;
            dtInfo.Message = options.IsTestRun
                ? "[DRY RUN] Would create element type"
                : "Created element type";
            result.DocumentTypes.Add(dtInfo);

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "DocumentType",
                $"{(options.IsTestRun ? "[DRY RUN] Would create" : "Created")} element type '{elementType.Alias}' for macro '{usage.Alias}'",
                null, elementType.Name, elementType.Key.ToString(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring element type for macro {Alias}", usage.Alias);
            dtInfo.ErrorMessage = ex.Message;
            result.DocumentTypes.Add(dtInfo);

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                "DocumentType", $"Error creating element type for macro '{usage.Alias}': {ex.Message}",
                ex.StackTrace, usage.Alias, cancellationToken: ct);
        }
    }

    private IContentType BuildElementTypeForMacro(IMacro macro)
    {
        var containerId = EnsureElementTypeContainer();
        var ct = new ContentType(_shortStringHelper, containerId)
        {
            Alias = macro.Alias,
            Name = macro.Name,
            Icon = "icon-settings-alt",
            IsElement = true
        };

        var parametersGroup = new PropertyGroup(new PropertyTypeCollection(ct.SupportsPublishing))
        {
            Alias = "parameters",
            Name = "Parameters",
            Type = PropertyGroupType.Tab,
            SortOrder = 0
        };
        ct.PropertyGroups.Add(parametersGroup);

        foreach (var property in macro.Properties)
        {
            var dataType = _dataTypeService.GetByEditorAlias(property.EditorAlias).FirstOrDefault();
            if (dataType == null)
            {
                continue; // skip; the user can wire up a data type after conversion
            }

            parametersGroup.PropertyTypes!.Add(new PropertyType(_shortStringHelper, dataType)
            {
                Alias = property.Alias,
                Name = property.Name
            });
        }

        return ct;
    }

    private int EnsureElementTypeContainer()
    {
        var existing = _contentTypeService.GetContainer(ElementTypeFolderKey);
        if (existing != null) return existing.Id;

        var attempt = _contentTypeService.CreateContainer(-1, ElementTypeFolderKey, ElementTypeFolderName);
        if (attempt.Success && attempt.Result?.Entity != null)
        {
            return attempt.Result.Entity.Id;
        }
        return -1;
    }

    /// <summary>
    /// Phase 3: ensure every macro-enabled Rich Text data type lists every selected macro's
    /// element type in its <c>Blocks</c> configuration.
    /// <para>Scope rules:</para>
    /// <list type="bullet">
    ///   <item>Only RTE data types whose toolbar configuration contains <c>umbmacro</c> are touched
    ///         — that's the developer's explicit "macros allowed here" signal. RTE data types without
    ///         the toolbar entry are never modified.</item>
    ///   <item>Phase 3 runs for <i>every</i> selected macro, whether Phase 2 created its element type
    ///         this run or reused an existing one. Running unconditionally makes the converter
    ///         self-correcting: if a previous run died between Phase 2 and Phase 3, the next run
    ///         finds the missing block configuration and fills it in.</item>
    ///   <item>Genuine idempotency comes from the per-data-type dedup: if the element type's key
    ///         is already in the data type's <c>Blocks</c> array, nothing is added and no save
    ///         happens. On a fully-synced environment Phase 3 is a verification pass with zero writes.</item>
    /// </list>
    /// Batched: at most one save per data type, even when multiple macros were converted.
    /// </summary>
    /// <remarks>
    /// Consequence to be aware of: if the developer manually removes a block from a data type's
    /// <c>Blocks</c> list but leaves <c>umbmacro</c> in the toolbar, Phase 3 will re-add the block
    /// on the next run because the toolbar still says "macros allowed here." To truly remove the
    /// block, also remove <c>umbmacro</c> from the toolbar — that's a coherent metadata-first signal.
    /// </remarks>
    private async Task ConfigureRichTextDataTypesAsync(
        ConversionOptions options,
        ConversionResult result,
        MacroConversionContext ctx,
        CancellationToken ct)
    {
        if (ctx.ElementTypesByMacroAlias.Count == 0)
        {
            // No macros selected (or all selected ones failed Phase 2). Nothing to verify.
            return;
        }

        var allSelectedElementTypes = ctx.ElementTypesByMacroAlias.Values.ToList();

        var rteDataTypes = _dataTypeService
            .GetByEditorAlias(Constants.PropertyEditors.Aliases.TinyMce)
            .ToList();

        foreach (var dataType in rteDataTypes)
        {
            ct.ThrowIfCancellationRequested();

            if (ctx.ConfiguredDataTypeIds.Contains(dataType.Id)) continue;

            var info = new DataTypeConversionInfo
            {
                Id = dataType.Id,
                Key = dataType.Key,
                Name = dataType.Name ?? string.Empty
            };

            try
            {
                if (dataType.Configuration is not RichTextConfiguration rteConfig)
                {
                    info.Skipped = true;
                    info.Message = "Data type is not a Rich Text configuration";
                    result.DataTypes.Add(info);
                    continue;
                }

                // Strict toolbar check — if the developer didn't enable umbmacro here, we
                // don't touch this RTE. Matches the "metadata is canonical" rule.
                if (!RichTextConfigurationInspector.HasUmbmacroInToolbar(rteConfig))
                {
                    info.Skipped = true;
                    info.Message = "Toolbar does not include 'umbmacro' — RTE does not allow macros; not modifying.";
                    result.DataTypes.Add(info);
                    continue;
                }

                var existingBlocks = rteConfig.Blocks?.ToList()
                    ?? new List<RichTextConfiguration.RichTextBlockConfiguration>();
                var existingKeys = existingBlocks.Select(b => b.ContentElementTypeKey).ToHashSet();
                var addedCount = 0;

                foreach (var elementType in allSelectedElementTypes)
                {
                    if (existingKeys.Add(elementType.Key))
                    {
                        existingBlocks.Add(new RichTextConfiguration.RichTextBlockConfiguration
                        {
                            ContentElementTypeKey = elementType.Key,
                            SettingsElementTypeKey = null
                        });
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    rteConfig.Blocks = existingBlocks.ToArray();
                    if (!options.IsTestRun)
                    {
                        using var scope = _scopeProvider.CreateScope();
                        _dataTypeService.Save(dataType);
                        scope.Complete();
                    }
                    info.Success = true;
                    info.Message = options.IsTestRun
                        ? $"[DRY RUN] Would attach {addedCount} element type(s)"
                        : $"Attached {addedCount} element type(s)";
                    ctx.ConfiguredDataTypeIds.Add(dataType.Id);

                    await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                        "DataType",
                        $"{(options.IsTestRun ? "[DRY RUN] Would configure" : "Configured")} Rich Text data type '{dataType.Name}' with {addedCount} macro element type(s)",
                        null, dataType.Name, dataType.Key.ToString(), ct);
                }
                else
                {
                    info.Skipped = true;
                    info.Message = "Verified — all selected element types are already registered on this data type";
                    info.Success = true;
                }

                result.DataTypes.Add(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring Rich Text data type {Name}", dataType.Name);
                info.ErrorMessage = ex.Message;
                result.DataTypes.Add(info);

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                    "DataType", $"Error configuring Rich Text data type '{dataType.Name}': {ex.Message}",
                    ex.StackTrace, dataType.Name, cancellationToken: ct);
            }
        }
    }

    /// <summary>
    /// Phase 4: Write a stub partial view for a converted macro at the conventional location,
    /// with migration instructions and the original macro code in a comment block.
    /// </summary>
    private async Task WriteStubPartialViewAsync(
        IMacro macro, MacroConversionContext ctx, ConversionResult result, CancellationToken ct)
    {
        var fileName = $"{macro.Name}.cshtml";
        if (!ctx.PartialsWritten.Add(fileName))
        {
            return;
        }

        // Don't overwrite an existing partial — assume the developer has already migrated it.
        var existing = _fileService.GetPartialView(PartialViewDirectory + fileName);
        if (existing != null)
        {
            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                "PartialView", $"Stub partial view already exists for macro '{macro.Name}'; not overwriting.",
                null, fileName, cancellationToken: ct);
            return;
        }

        var originalMacroPartial = _fileService.GetPartialViewMacro(fileName);
        var originalContent = originalMacroPartial?.Content ?? "<!-- Original macro view not found -->";

        var macroProperties = macro.Properties.Cast<IMacroProperty>().ToList();
        var propertyList = macroProperties.Count > 0
            ? string.Join("\n", macroProperties.Select(p => $"    - {p.Alias} ({p.EditorAlias})"))
            : "    (No properties defined)";

        var stubBuilder = new StringBuilder();
        stubBuilder.AppendLine("@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.RichTextBlockItem>");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("@*");
        stubBuilder.AppendLine("    THIS VIEW NEEDS TO BE UPDATED");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine($"    Auto-generated during macro to block conversion for macro: {macro.Name}");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("    REQUIRED CHANGES:");
        stubBuilder.AppendLine("    1. Replace Model.MacroParameters[\"x\"] with Model.Content.Value<T>(\"x\").");
        stubBuilder.AppendLine("    2. Update any @using statements if necessary.");
        stubBuilder.AppendLine("    3. Test rendering in the backoffice and on the frontend.");
        stubBuilder.AppendLine("    4. Remove this header once migration is complete.");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("    Available block properties:");
        stubBuilder.AppendLine(propertyList);
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("    Migration examples:");
        stubBuilder.AppendLine("      Old: Model.MacroParameters[\"title\"]");
        stubBuilder.AppendLine("      New: Model.Content.Value<string>(\"title\")");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("    ============================================");
        stubBuilder.AppendLine("    Original macro view code (for reference):");
        stubBuilder.AppendLine("    ============================================");
        stubBuilder.AppendLine(originalContent);
        stubBuilder.AppendLine("    ============================================");
        stubBuilder.AppendLine("*@");
        stubBuilder.AppendLine();
        stubBuilder.AppendLine("<div class=\"block-migration-warning\" style=\"padding: 20px; background: #fff3cd; border: 2px solid #ffc107; border-radius: 4px; margin: 10px 0;\">");
        stubBuilder.AppendLine($"  <h4 style=\"margin-top: 0; color: #856404;\">Block requires configuration</h4>");
        stubBuilder.AppendLine($"  <p style=\"margin-bottom: 0; color: #856404;\">This block was auto-converted from the macro <strong>{macro.Name}</strong>. The view template must be updated by a developer before it will render correctly. See <code>{PartialViewDirectory + fileName}</code> for migration instructions.</p>");
        stubBuilder.AppendLine("</div>");

        _fileService.SavePartialView(new Cms.Core.Models.PartialView(Cms.Core.Models.PartialViewType.PartialView, PartialViewDirectory + fileName)
        {
            Content = stubBuilder.ToString()
        });

        await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
            "PartialView", $"Created stub partial view for macro '{macro.Name}' at {PartialViewDirectory + fileName}",
            null, fileName, cancellationToken: ct);
    }

    /// <summary>
    /// Phase 5: rewrite a single content node — every macro-enabled Rich Text property and
    /// every macro-enabled nested Rich Text property inside a Block List/Grid.
    /// <para>Properties whose RTE data type doesn't have <c>umbmacro</c> in its toolbar are
    /// skipped entirely, even if they happen to contain macro markup — that's content drift
    /// the developer needs to fix at source; the converter would just undo the next uSync.</para>
    /// </summary>
    private async Task RewriteContentNodeAsync(
        IContent content,
        HashSet<string> selectedAliases,
        HashSet<string> knownAliases,
        ConversionOptions options,
        ConversionResult result,
        MacroConversionContext ctx,
        CancellationToken ct)
    {
        var contentInfo = new ContentConversionInfo
        {
            Id = content.Id,
            Key = content.Key,
            Name = content.Name ?? string.Empty
        };

        try
        {
            var docType = _contentTypeService.Get(content.ContentTypeId);
            if (docType == null)
            {
                contentInfo.Skipped = true;
                contentInfo.Message = "Content type not found";
                result.ContentNodes.Add(contentInfo);
                return;
            }

            // For each property: editor alias and (for RTEs) whether its data type is macro-enabled.
            // DistinctBy defends against duplicate aliases in CompositionPropertyTypes — see
            // MacroConverterQueryService.ScanBlockValueForMacros for the diamond-inheritance rationale.
            var propertyInfoByAlias = docType.CompositionPropertyTypes
                .DistinctBy(pt => pt.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    pt => pt.Alias,
                    pt => (
                        EditorAlias: pt.PropertyEditorAlias,
                        MacroEnabled: IsRichTextDataTypeMacroEnabled(pt.DataTypeId)
                    ),
                    StringComparer.OrdinalIgnoreCase);

            var modified = false;
            var orphans = new RewriteSummary();

            foreach (var property in content.Properties)
            {
                if (!propertyInfoByAlias.TryGetValue(property.Alias, out var info)) continue;

                var cultures = property.PropertyType.Variations.HasFlag(ContentVariation.Culture)
                    ? property.Values.Select(v => v.Culture).Where(c => c != null).ToList()
                    : new List<string?> { null };

                foreach (var culture in cultures)
                {
                    var raw = property.GetValue(culture)?.ToString();
                    if (string.IsNullOrEmpty(raw)) continue;

                    string? newValue = null;
                    if (string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.TinyMce, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!info.MacroEnabled)
                        {
                            // RTE without umbmacro — leave content alone. Metadata is canonical.
                            continue;
                        }
                        newValue = RewriteRichTextValue(raw, selectedAliases, knownAliases, ctx, orphans, property.Alias);
                    }
                    else if (string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.BlockList, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.BlockGrid, StringComparison.OrdinalIgnoreCase))
                    {
                        newValue = RewriteBlockValue(raw, selectedAliases, knownAliases, ctx, orphans, property.Alias);
                    }

                    if (!string.IsNullOrEmpty(newValue) && newValue != raw)
                    {
                        property.SetValue(newValue, culture);
                        modified = true;
                        contentInfo.PropertiesConverted++;
                    }
                }
            }

            if (orphans.OrphanAliases.Count > 0)
            {
                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Warning,
                    "Content",
                    $"Orphan macro reference(s) on '{content.Name}': aliases [{string.Join(", ", orphans.OrphanAliases)}] in properties [{string.Join(", ", orphans.OrphanProperties)}]. " +
                    "These macros have no IMacro definition and were left in place. Re-register the macro(s) in Umbraco's Settings → Macros and re-run if you want them converted.",
                    null, content.Name, content.Key.ToString(), ct);
            }

            if (modified)
            {
                if (!options.IsTestRun)
                {
                    using var scope = _scopeProvider.CreateScope();
                    if (options.PublishAfterConversion)
                    {
                        _contentService.SaveAndPublish(content);
                    }
                    else
                    {
                        _contentService.Save(content);
                    }
                    scope.Complete();
                }

                contentInfo.Success = true;
                contentInfo.Message = $"{(options.IsTestRun ? "[DRY RUN] Would rewrite" : "Rewrote")} {contentInfo.PropertiesConverted} value(s)";

                await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
                    "Content",
                    $"{(options.IsTestRun ? "[DRY RUN] Would convert" : "Converted")} {contentInfo.PropertiesConverted} value(s) on '{content.Name}'",
                    null, content.Name, content.Key.ToString(), ct);
            }
            else
            {
                contentInfo.Skipped = true;
                contentInfo.Message = orphans.OrphanAliases.Count > 0
                    ? "Only orphan markup found; nothing rewritten"
                    : "No values needed rewriting";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rewriting content {Name} ({Id})", content.Name, content.Id);
            contentInfo.ErrorMessage = ex.Message;

            await _historyService.LogEntryAsync(result.ConversionId, LogLevel.Error,
                "Content", $"Error rewriting content '{content.Name}': {ex.Message}",
                ex.StackTrace, content.Name, content.Key.ToString(), ct);

            if (options.StopOnError)
            {
                result.ContentNodes.Add(contentInfo);
                throw;
            }
        }

        result.ContentNodes.Add(contentInfo);
    }

    /// <summary>
    /// Returns true when the supplied data type ID is an RTE whose toolbar contains <c>umbmacro</c>.
    /// Used by the per-property filter in <see cref="RewriteContentNodeAsync"/>.
    /// </summary>
    private bool IsRichTextDataTypeMacroEnabled(int dataTypeId)
    {
        var dataType = _dataTypeService.GetDataType(dataTypeId);
        if (dataType == null) return false;
        if (!string.Equals(dataType.EditorAlias, Constants.PropertyEditors.Aliases.TinyMce, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return dataType.Configuration is RichTextConfiguration rteConfig
               && RichTextConfigurationInspector.HasUmbmacroInToolbar(rteConfig);
    }

    /// <summary>
    /// Accumulator for orphan macro references encountered while rewriting one content node.
    /// </summary>
    private sealed class RewriteSummary
    {
        public HashSet<string> OrphanAliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OrphanProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rewrites one Rich Text property value, replacing every selected macro markup with a
    /// <c>&lt;umb-rte-block&gt;</c> entry and recording the new block in the value's <c>Blocks</c>.
    /// Returns null if no changes were made. Records orphan aliases on <paramref name="summary"/>.
    /// </summary>
    private string? RewriteRichTextValue(
        string rawValue,
        HashSet<string> selectedAliases,
        HashSet<string> knownAliases,
        MacroConversionContext ctx,
        RewriteSummary summary,
        string propertyAlias)
    {
        if (!RichTextPropertyEditorHelper.TryParseRichTextEditorValue(
                rawValue, _jsonSerializer, _logger, out var rteValue))
        {
            // Raw markup form: wrap into a value model so we can attach blocks.
            rteValue = new RichTextEditorValue { Markup = rawValue, Blocks = null };
        }

        var markup = rteValue!.Markup ?? string.Empty;
        if (!MacroMarkupParser.HasMacro(markup))
        {
            return null;
        }

        var anyChanges = false;

        foreach (var macroString in MacroMarkupParser.GetMacroStrings(markup))
        {
            if (!MacroMarkupParser.TryGetMacroAlias(macroString, out var alias))
            {
                continue;
            }
            if (!knownAliases.Contains(alias))
            {
                // Orphan — no IMacro definition. Leave markup in place, surface as warning later.
                summary.OrphanAliases.Add(alias);
                summary.OrphanProperties.Add(propertyAlias);
                continue;
            }
            if (!selectedAliases.Contains(alias))
            {
                // Known macro the user chose not to convert — silent skip.
                continue;
            }
            if (!ctx.ElementTypesByMacroAlias.TryGetValue(alias, out var elementType))
            {
                continue; // Phase 2 failed for this macro
            }

            var parameters = MacroMarkupParser.GetParametersFromMacro(macroString);
            var blockKey = Guid.NewGuid();
#pragma warning disable CS0618 // Udi obsolete but still required for v13 serialization
            var blockUdi = new GuidUdi("element", blockKey);
#pragma warning restore CS0618

            rteValue.Blocks ??= new BlockValue();
#pragma warning disable CS0618 // RawPropertyValues / Udi obsolete in v13
            rteValue.Blocks.ContentData.Add(new BlockItemData
            {
                Udi = blockUdi,
                ContentTypeKey = elementType.Key,
                ContentTypeAlias = elementType.Alias,
                RawPropertyValues = parameters
                    .Where(p => !p.Key.Equals("macroAlias", StringComparison.OrdinalIgnoreCase)
                             && !p.Key.Equals("alias", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(p => p.Key, p => (object?)p.Value)
            });
#pragma warning restore CS0618

            rteValue.Blocks.Layout ??= new Dictionary<string, JToken>();
            var layoutItems = new List<RichTextBlockLayoutItem>();
            if (rteValue.Blocks.Layout.TryGetValue(Constants.PropertyEditors.Aliases.TinyMce, out var existingLayout))
            {
                layoutItems = existingLayout.ToObject<List<RichTextBlockLayoutItem>>() ?? new List<RichTextBlockLayoutItem>();
            }

#pragma warning disable CS0618 // ContentUdi obsolete in v13
            layoutItems.Add(new RichTextBlockLayoutItem { ContentUdi = blockUdi });
#pragma warning restore CS0618
            rteValue.Blocks.Layout[Constants.PropertyEditors.Aliases.TinyMce] = JToken.FromObject(layoutItems);

            // Replace the first occurrence only — distinct keys per occurrence keep
            // ContentData/Layout aligned with the markup positions.
            var idx = markup.IndexOf(macroString, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var replacement = $"<umb-rte-block data-content-key=\"{blockKey:D}\"><!--Umbraco-Block--></umb-rte-block>";
                markup = markup.Substring(0, idx) + replacement + markup.Substring(idx + macroString.Length);
                anyChanges = true;
            }
        }

        if (!anyChanges)
        {
            return null;
        }

        rteValue.Markup = markup;
        return RichTextPropertyEditorHelper.SerializeRichTextEditorValue(rteValue, _jsonSerializer);
    }

    /// <summary>
    /// Walks a Block List / Block Grid value's <c>contentData</c> array, recursively rewriting
    /// any nested Rich Text property values that contain selected macros, when the nested RTE's
    /// data type allows macros (toolbar contains <c>umbmacro</c>).
    /// </summary>
    private string? RewriteBlockValue(
        string rawValue,
        HashSet<string> selectedAliases,
        HashSet<string> knownAliases,
        MacroConversionContext ctx,
        RewriteSummary summary,
        string propertyAlias)
    {
        JObject root;
        try
        {
            root = JObject.Parse(rawValue);
        }
        catch
        {
            return null;
        }

        var contentData = root["contentData"] as JArray;
        if (contentData == null || contentData.Count == 0)
        {
            return null;
        }

        var anyChanges = false;

        foreach (var element in contentData.OfType<JObject>())
        {
            var contentTypeKeyStr = element.Value<string>("contentTypeKey");
            if (!Guid.TryParse(contentTypeKeyStr, out var contentTypeKey)) continue;

            var blockContentType = _contentTypeService.Get(contentTypeKey);
            if (blockContentType == null) continue;

            // DistinctBy defends against duplicate aliases — see ScanBlockValueForMacros for context.
            var byAlias = blockContentType.CompositionPropertyTypes
                .DistinctBy(pt => pt.Alias, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    pt => pt.Alias,
                    pt => (EditorAlias: pt.PropertyEditorAlias, DataTypeId: pt.DataTypeId),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var prop in element.Properties().ToList())
            {
                if (!byAlias.TryGetValue(prop.Name, out var info)) continue;

                var inner = prop.Value?.ToString();
                if (string.IsNullOrEmpty(inner)) continue;

                string? rewritten = null;
                var nestedAlias = $"{propertyAlias}>{prop.Name}";

                if (string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.TinyMce, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsRichTextDataTypeMacroEnabled(info.DataTypeId))
                    {
                        // Nested RTE doesn't allow macros — same metadata-is-canonical rule.
                        continue;
                    }
                    rewritten = RewriteRichTextValue(inner, selectedAliases, knownAliases, ctx, summary, nestedAlias);
                }
                else if (string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.BlockList, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(info.EditorAlias, Constants.PropertyEditors.Aliases.BlockGrid, StringComparison.OrdinalIgnoreCase))
                {
                    rewritten = RewriteBlockValue(inner, selectedAliases, knownAliases, ctx, summary, nestedAlias);
                }

                if (rewritten != null)
                {
                    element[prop.Name] = rewritten;
                    anyChanges = true;
                }
            }
        }

        return anyChanges ? root.ToString(Newtonsoft.Json.Formatting.None) : null;
    }

    private Task LogPhaseAsync(ConversionResult result, string message, CancellationToken ct)
        => _historyService.LogEntryAsync(result.ConversionId, LogLevel.Information,
            "Conversion", message, null, cancellationToken: ct);

    private static void ReportProgress(
        IProgress<ConversionProgress>? progress,
        Guid conversionId,
        string phase,
        string currentItem,
        int processedCount,
        int totalCount)
    {
        progress?.Report(new ConversionProgress
        {
            ConversionId = conversionId,
            Phase = phase,
            CurrentItem = currentItem,
            ProcessedCount = processedCount,
            TotalCount = totalCount,
            Status = ConversionStatus.Running
        });
    }
}
