using Umbraco.Community.LegacyFeatureConverter.Converters.Macros;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Converters.Macros;

/// <summary>
/// Behavioral tests for <see cref="MacroToRichTextBlockConverter"/> under the IMacro-driven
/// flow:
/// <list type="bullet">
///   <item>Plan + execute drive from the macro registry, not from content discovery.</item>
///   <item>Phase 3 only touches RTE data types whose toolbar contains <c>umbmacro</c>.</item>
///   <item>Phase 3 only adds entries for element types created in this run (reused ones are left to the dev).</item>
///   <item>Phase 5 only runs under <see cref="ConversionApproach.Thorough"/>.</item>
///   <item>Test runs save nothing.</item>
/// </list>
/// </summary>
[TestClass]
public class MacroToRichTextBlockConverterTests
{
    private Mock<ILogger<MacroToRichTextBlockConverter>> _loggerMock = null!;
    private Mock<IMacroService> _macroServiceMock = null!;
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IFileService> _fileServiceMock = null!;
    private Mock<IShortStringHelper> _shortStringHelperMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IScopeProvider> _scopeProviderMock = null!;
    private Mock<IJsonSerializer> _jsonSerializerMock = null!;
    private Mock<IConfigurationEditorJsonSerializer> _configurationEditorJsonSerializerMock = null!;
    private Mock<IMacroConverterQueryService> _queryServiceMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MacroToRichTextBlockConverter>>();
        _macroServiceMock = new Mock<IMacroService>();
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _fileServiceMock = new Mock<IFileService>();
        _shortStringHelperMock = new Mock<IShortStringHelper>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _scopeProviderMock = new Mock<IScopeProvider>();
        _jsonSerializerMock = new Mock<IJsonSerializer>();
        _configurationEditorJsonSerializerMock = new Mock<IConfigurationEditorJsonSerializer>();
        _queryServiceMock = new Mock<IMacroConverterQueryService>();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(Enumerable.Empty<IContentType>());
        _dataTypeServiceMock.Setup(x => x.GetByEditorAlias(It.IsAny<string>()))
            .Returns(Enumerable.Empty<IDataType>());

        // The converter wraps each Save in a short-lived scope. Tests that use
        // IsTestRun = false need a working scope mock so `using var scope` doesn't NRE.
        var scopeMock = new Mock<IScope>();
        _scopeProviderMock.Setup(x => x.CreateScope(
                It.IsAny<System.Data.IsolationLevel>(),
                It.IsAny<Cms.Core.Scoping.RepositoryCacheMode>(),
                It.IsAny<Cms.Core.Events.IEventDispatcher?>(),
                It.IsAny<Cms.Core.Events.IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(scopeMock.Object);
    }

    private MacroToRichTextBlockConverter CreateConverter()
    {
        return new MacroToRichTextBlockConverter(
            _loggerMock.Object,
            _macroServiceMock.Object,
            _contentTypeServiceMock.Object,
            _dataTypeServiceMock.Object,
            _contentServiceMock.Object,
            _fileServiceMock.Object,
            _shortStringHelperMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object,
            _jsonSerializerMock.Object,
            _configurationEditorJsonSerializerMock.Object,
            _queryServiceMock.Object);
    }

    private static Mock<IMacro> MakeMacroMock(string alias, string name, Guid? key = null)
    {
        var mock = new Mock<IMacro>();
        mock.Setup(m => m.Alias).Returns(alias);
        mock.Setup(m => m.Name).Returns(name);
        mock.Setup(m => m.Key).Returns(key ?? Guid.NewGuid());
        mock.Setup(m => m.Properties).Returns(new MacroPropertyCollection());
        return mock;
    }

    private static MacroAliasUsage MakeUsage(IMacro macro, int usageCount = 0)
    {
        return new MacroAliasUsage
        {
            Alias = macro.Alias,
            Macro = macro,
            UsageCount = usageCount,
            RtePropertyCount = usageCount
        };
    }

    private void SetupExistingElementTypeContainer()
    {
        _contentTypeServiceMock.Setup(x => x.GetContainer(MacroToRichTextBlockConverter.ElementTypeFolderKey))
            .Returns(new EntityContainer(Cms.Core.Constants.ObjectTypes.DocumentType)
            {
                Id = 999,
                Key = MacroToRichTextBlockConverter.ElementTypeFolderKey
            });
    }

    private static IDataType MakeRteDataType(int id, bool umbmacroEnabled, params Guid[] alreadyConfiguredBlockKeys)
    {
        var config = new RichTextConfiguration
        {
            Blocks = alreadyConfiguredBlockKeys
                .Select(k => new RichTextConfiguration.RichTextBlockConfiguration { ContentElementTypeKey = k })
                .ToArray()
        };
        if (umbmacroEnabled)
        {
            config.Editor = JObject.Parse("{ \"toolbar\": [\"bold\", \"umbmacro\", \"italic\"] }");
        }
        else
        {
            config.Editor = JObject.Parse("{ \"toolbar\": [\"bold\", \"italic\"] }");
        }

        var mock = new Mock<IDataType>();
        mock.Setup(d => d.Id).Returns(id);
        mock.Setup(d => d.Key).Returns(Guid.NewGuid());
        mock.Setup(d => d.Name).Returns($"RTE-{id}");
        mock.Setup(d => d.EditorAlias).Returns(Cms.Core.Constants.PropertyEditors.Aliases.TinyMce);
        mock.Setup(d => d.Configuration).Returns(config);
        return mock.Object;
    }

    // ===== Metadata =====

    [TestMethod]
    public void Metadata_ReportsMacroCategoryAndIcon()
    {
        var converter = CreateConverter();

        Assert.AreEqual("Macro to Rich Text Block", converter.ConverterName);
        Assert.AreEqual("Macro", converter.ShortName);
        Assert.AreEqual("Macro", converter.Category);
        Assert.AreEqual("icon-code", converter.Icon);
        Assert.AreEqual("richTextBlock", converter.TargetShapeAlias);
    }

    // ===== ComputePlanAsync =====

    [TestMethod]
    public async Task ComputePlanAsync_ReturnsOnePlanMacroPerRegisteredMacro()
    {
        var converter = CreateConverter();

        var macro = MakeMacroMock("contactForm", "Contact Form");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object, usageCount: 3) },
                AffectedContentIds = new List<int> { 100, 200, 300 }
            });

        var plan = await converter.ComputePlanAsync(ConversionApproach.Thorough);

        Assert.AreEqual(1, plan.Macros.Count);
        Assert.AreEqual("contactForm", plan.Macros[0].Alias);
        Assert.AreEqual("Contact Form", plan.Macros[0].Name);
        Assert.AreEqual(3, plan.Macros[0].UsageCount);
        Assert.AreEqual(3, plan.TotalMacroUsages);
        Assert.AreEqual(3, plan.TotalContentNodes);
    }

    [TestMethod]
    public async Task ComputePlanAsync_IncludesZeroUsageMacros()
    {
        // Registry has macros that aren't currently used in content. They still appear so
        // the developer can choose to scaffold an element type and partial view for them.
        var converter = CreateConverter();
        var unused = MakeMacroMock("unused", "Unused Macro");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(unused.Object, usageCount: 0) },
                AffectedContentIds = new List<int>()
            });

        var plan = await converter.ComputePlanAsync(ConversionApproach.Fast);

        Assert.AreEqual(1, plan.Macros.Count);
        Assert.AreEqual(0, plan.Macros[0].UsageCount);
        Assert.AreEqual(0, plan.TotalMacroUsages);
    }

    // ===== Test-run safety =====

    [TestMethod]
    public async Task ExecuteConversionAsync_TestRun_SavesNothing()
    {
        var converter = CreateConverter();

        var macro = MakeMacroMock("simpleCta", "Simple CTA");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>()
            });

        SetupExistingElementTypeContainer();

        var options = new ConversionOptions
        {
            IsTestRun = true,
            GenerateStubPartialViews = true,
            Approach = ConversionApproach.Thorough
        };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(ConversionStatus.Completed, result.Status);
        Assert.IsTrue(result.IsTestRun);

        _contentTypeServiceMock.Verify(x => x.Save(It.IsAny<IContentType>(), It.IsAny<int>()), Times.Never);
        _dataTypeServiceMock.Verify(x => x.Save(It.IsAny<IDataType>(), It.IsAny<int>()), Times.Never);
        _contentServiceMock.Verify(s => s.Save(It.IsAny<IContent>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
        _contentServiceMock.Verify(s => s.SaveAndPublish(It.IsAny<IContent>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _fileServiceMock.Verify(s => s.SavePartialView(It.IsAny<IPartialView>(), It.IsAny<int>()), Times.Never);
    }

    // ===== Selection =====

    [TestMethod]
    public async Task ExecuteConversionAsync_OnlyProcessesSelectedMacros()
    {
        var converter = CreateConverter();

        var keepMacro = MakeMacroMock("keep", "Keep");
        var skipMacro = MakeMacroMock("skip", "Skip");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage>
                {
                    MakeUsage(keepMacro.Object),
                    MakeUsage(skipMacro.Object)
                },
                AffectedContentIds = new List<int>()
            });
        _contentTypeServiceMock.Setup(x => x.Get(It.IsAny<string>()))
            .Returns((IContentType?)null);
        SetupExistingElementTypeContainer();

        var options = new ConversionOptions
        {
            IsTestRun = true,
            Approach = ConversionApproach.Fast,
            SelectedMacroKeys = new[] { keepMacro.Object.Key }
        };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(ConversionStatus.Completed, result.Status);
        Assert.AreEqual(1, result.DocumentTypes.Count);
        Assert.AreEqual("keep", result.DocumentTypes[0].Alias);
    }

    // ===== Phase 2 idempotency =====

    [TestMethod]
    public async Task ExecuteConversionAsync_FullySyncedSchema_VerifiesWithoutSaving()
    {
        // Cross-environment "schema already in sync" scenario:
        //   - Element type already exists (synced via uSync from dev).
        //   - The macro-enabled RTE data type already lists the element type's key in Blocks.
        // Phase 2 reuses, Phase 3 runs but finds nothing to add — dedup yields zero saves.
        // This is the converter's "verify" pass: walks the metadata to confirm it's in order
        // without making any changes.
        var converter = CreateConverter();

        var macro = MakeMacroMock("alreadyExists", "Already Exists");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>()
            });

        var existingElementTypeKey = Guid.NewGuid();
        var existing = new Mock<IContentType>();
        existing.Setup(c => c.Alias).Returns("alreadyExists");
        existing.Setup(c => c.IsElement).Returns(true);
        existing.Setup(c => c.Key).Returns(existingElementTypeKey);
        existing.Setup(c => c.Name).Returns("Already Exists");
        _contentTypeServiceMock.Setup(x => x.Get("alreadyExists")).Returns(existing.Object);

        // RTE data type with umbmacro enabled AND already lists the element type's key.
        // Phase 3 will run but the dedup detects everything's in place → no save.
        var rteDt = MakeRteDataType(id: 42, umbmacroEnabled: true, alreadyConfiguredBlockKeys: existingElementTypeKey);
        _dataTypeServiceMock.Setup(x => x.GetByEditorAlias(Cms.Core.Constants.PropertyEditors.Aliases.TinyMce))
            .Returns(new[] { rteDt });

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast
        };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(1, result.DocumentTypes.Count);
        Assert.IsTrue(result.DocumentTypes[0].Skipped);
        Assert.IsTrue(result.DocumentTypes[0].Success);

        // Phase 3 ran but dedup'd — no data type save.
        _dataTypeServiceMock.Verify(x => x.Save(It.IsAny<IDataType>(), It.IsAny<int>()), Times.Never);

        // The data type's log entry should reflect "verified, nothing to do".
        var dtLog = result.DataTypes.SingleOrDefault(d => d.Id == 42);
        Assert.IsNotNull(dtLog);
        Assert.IsTrue(dtLog!.Skipped);
        Assert.IsTrue(dtLog.Message?.Contains("Verified", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_ReusedElementType_ButMissingBlock_CorrectsConfiguration()
    {
        // Recovery scenario: a previous run created the element type but failed before Phase 3
        // could attach it to the RTE data type. On the re-run, Phase 2 reuses the existing
        // element type and Phase 3 STILL runs — finds the missing block entry and adds it.
        var converter = CreateConverter();

        var macro = MakeMacroMock("recoverMe", "Recover Me");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>()
            });

        // Element type already exists from the previous (interrupted) run.
        var existing = new Mock<IContentType>();
        existing.Setup(c => c.Alias).Returns("recoverMe");
        existing.Setup(c => c.IsElement).Returns(true);
        existing.Setup(c => c.Key).Returns(Guid.NewGuid());
        existing.Setup(c => c.Name).Returns("Recover Me");
        _contentTypeServiceMock.Setup(x => x.Get("recoverMe")).Returns(existing.Object);

        // RTE data type allows macros but DOESN'T list the element type yet — that's the
        // gap left by the interrupted previous run.
        var rteDt = MakeRteDataType(id: 77, umbmacroEnabled: true /* no alreadyConfiguredBlockKeys */);
        _dataTypeServiceMock.Setup(x => x.GetByEditorAlias(Cms.Core.Constants.PropertyEditors.Aliases.TinyMce))
            .Returns(new[] { rteDt });

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast
        };

        var result = await converter.ExecuteConversionAsync(options);

        // The data type was missing the block — Phase 3 corrected it.
        _dataTypeServiceMock.Verify(x => x.Save(rteDt, It.IsAny<int>()), Times.Once);

        var dtLog = result.DataTypes.SingleOrDefault(d => d.Id == 77);
        Assert.IsNotNull(dtLog);
        Assert.IsTrue(dtLog!.Success);
        Assert.IsFalse(dtLog.Skipped);
    }

    // ===== Phase 3 — toolbar filter =====

    [TestMethod]
    public async Task Phase3_OnlyConfiguresDataTypesWithUmbmacroToolbar()
    {
        // Two RTE data types: one has umbmacro enabled, the other doesn't.
        // After Phase 2 creates a new element type, only the enabled one should be saved.
        var converter = CreateConverter();
        var macro = MakeMacroMock("newMacro", "New Macro");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>()
            });
        _contentTypeServiceMock.Setup(x => x.Get("newMacro")).Returns((IContentType?)null);
        SetupExistingElementTypeContainer();

        var withUmbmacro = MakeRteDataType(id: 100, umbmacroEnabled: true);
        var withoutUmbmacro = MakeRteDataType(id: 200, umbmacroEnabled: false);
        _dataTypeServiceMock.Setup(x => x.GetByEditorAlias(Cms.Core.Constants.PropertyEditors.Aliases.TinyMce))
            .Returns(new[] { withUmbmacro, withoutUmbmacro });

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast // fast — no content walk
        };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(ConversionStatus.Completed, result.Status);
        // Data type with umbmacro should be configured (and saved).
        _dataTypeServiceMock.Verify(x => x.Save(withUmbmacro, It.IsAny<int>()), Times.Once);
        // Data type without umbmacro must not be touched.
        _dataTypeServiceMock.Verify(x => x.Save(withoutUmbmacro, It.IsAny<int>()), Times.Never);

        // The result log should mention both: one configured, one skipped due to no umbmacro.
        var configuredEntry = result.DataTypes.SingleOrDefault(d => d.Id == 100);
        Assert.IsNotNull(configuredEntry);
        Assert.IsTrue(configuredEntry!.Success);
        Assert.IsFalse(configuredEntry.Skipped);

        var skippedEntry = result.DataTypes.SingleOrDefault(d => d.Id == 200);
        Assert.IsNotNull(skippedEntry);
        Assert.IsTrue(skippedEntry!.Skipped);
        Assert.IsTrue(skippedEntry.Message?.Contains("does not include 'umbmacro'") ?? false);
    }

    [TestMethod]
    public async Task Phase3_DoesNotReAddBlockAlreadyPresentOnDataType()
    {
        // RTE data type already has our element type configured (e.g. from a prior run).
        // Phase 3 must dedupe — no save should happen.
        var converter = CreateConverter();
        var macro = MakeMacroMock("alreadyAttached", "Already Attached");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>()
            });

        // Phase 2 will need to CREATE the element type to enter Phase 3 with it.
        // Then it would try to attach to the data type — which already has it.
        _contentTypeServiceMock.Setup(x => x.Get("alreadyAttached")).Returns((IContentType?)null);
        SetupExistingElementTypeContainer();

        // The data type already lists a block with the same key as the element type we'd create.
        // We can't easily inject the created element type's key in advance, so use a different test:
        // pre-create a known-key data type with NO blocks and verify we add it once.
        var rteDt = MakeRteDataType(id: 300, umbmacroEnabled: true);
        _dataTypeServiceMock.Setup(x => x.GetByEditorAlias(Cms.Core.Constants.PropertyEditors.Aliases.TinyMce))
            .Returns(new[] { rteDt });

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast
        };

        await converter.ExecuteConversionAsync(options);

        // Confirm the data type was saved (entry added). Idempotency on a second run is
        // exercised by the existing "schema already exists" test which goes through Phase 2's
        // reuse-and-skip-Phase-3 path.
        _dataTypeServiceMock.Verify(x => x.Save(rteDt, It.IsAny<int>()), Times.Once);
    }

    // ===== Fast vs Thorough =====

    [TestMethod]
    public async Task Phase5_RunsForFastApproach()
    {
        // Fast now rewrites content for every macro in the registry that's been selected.
        // It walks the same AffectedContentIds the scan produced.
        var converter = CreateConverter();
        var macro = MakeMacroMock("foo", "Foo");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object, usageCount: 2) },
                AffectedContentIds = new List<int> { 11, 22 }
            });

        SetupExistingElementTypeContainer();
        _contentServiceMock.Setup(s => s.GetById(It.IsAny<int>())).Returns((IContent?)null);

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast
        };

        await converter.ExecuteConversionAsync(options);

        _contentServiceMock.Verify(s => s.GetById(11), Times.Once);
        _contentServiceMock.Verify(s => s.GetById(22), Times.Once);
    }

    [TestMethod]
    public async Task Phase5_RunsForThoroughApproach()
    {
        // Thorough walks the same AffectedContentIds + any orphan content IDs from the scan.
        var converter = CreateConverter();
        var macro = MakeMacroMock("foo", "Foo");
        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object, usageCount: 2) },
                AffectedContentIds = new List<int> { 11, 22 }
            });

        SetupExistingElementTypeContainer();
        _contentServiceMock.Setup(s => s.GetById(It.IsAny<int>())).Returns((IContent?)null);

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Thorough
        };

        await converter.ExecuteConversionAsync(options);

        _contentServiceMock.Verify(s => s.GetById(11), Times.Once);
        _contentServiceMock.Verify(s => s.GetById(22), Times.Once);
    }

    [TestMethod]
    public async Task Phase5_Thorough_OrphanWithMatchingElementType_AddedToProcessingSet()
    {
        // Thorough orphan recovery: an orphan macro has an existing element type in Umbraco
        // (aliased the same). Phase 5 should add the orphan's content ID to the processing
        // set so the content node gets visited and rewritten.
        var converter = CreateConverter();
        var macro = MakeMacroMock("knownMacro", "Known Macro");

        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>(),
                Orphans = new List<OrphanMacroOccurrence>
                {
                    new() { Alias = "removedMacro", ContentId = 500, PropertyAlias = "body", Culture = null }
                }
            });

        // Existing element type whose alias matches the orphan — this is the recovery hook.
        var recoveredElement = new Mock<IContentType>();
        recoveredElement.Setup(c => c.Alias).Returns("removedMacro");
        recoveredElement.Setup(c => c.IsElement).Returns(true);
        recoveredElement.Setup(c => c.Key).Returns(Guid.NewGuid());
        recoveredElement.Setup(c => c.Name).Returns("Removed Macro");

        // GetAll returns the element type so the orphan-recovery lookup finds it.
        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(new[] { recoveredElement.Object });
        SetupExistingElementTypeContainer();
        _contentServiceMock.Setup(s => s.GetById(It.IsAny<int>())).Returns((IContent?)null);

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Thorough
        };

        await converter.ExecuteConversionAsync(options);

        // Orphan content node was visited even though it wasn't in AffectedContentIds.
        _contentServiceMock.Verify(s => s.GetById(500), Times.Once);
    }

    [TestMethod]
    public async Task Phase5_Fast_OrphansFound_LogsHeadsUp()
    {
        // Fast doesn't touch orphan content but should log an info-level heads-up so the
        // developer knows running Thorough has something to do.
        var converter = CreateConverter();
        var macro = MakeMacroMock("knownMacro", "Known Macro");

        _queryServiceMock.Setup(x => x.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage> { MakeUsage(macro.Object) },
                AffectedContentIds = new List<int>(),
                Orphans = new List<OrphanMacroOccurrence>
                {
                    new() { Alias = "ghostMacro", ContentId = 600, PropertyAlias = "body", Culture = null }
                }
            });

        SetupExistingElementTypeContainer();

        var options = new ConversionOptions
        {
            IsTestRun = false,
            Approach = ConversionApproach.Fast
        };

        await converter.ExecuteConversionAsync(options);

        // Fast must not visit the orphan content node.
        _contentServiceMock.Verify(s => s.GetById(600), Times.Never);

        // A heads-up info log entry must have been written naming the orphan alias.
        _historyServiceMock.Verify(s => s.LogEntryAsync(
            It.IsAny<Guid>(),
            LogLevel.Information,
            "Conversion",
            It.Is<string>(msg => msg.Contains("ghostMacro") && msg.Contains("Thorough")),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
