using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Converters;

/// <summary>
/// Concrete instrumentable subclass of <see cref="BaseMacroConverter"/> used to verify
/// the shared lifecycle wrapper (history, cancellation, exception handling, status derivation).
/// </summary>
internal class TestableMacroConverter : BaseMacroConverter
{
    public Func<ConversionOptions, ConversionResult, IProgress<ConversionProgress>?, CancellationToken, Task>? CoreHandler { get; set; }
    public Func<ConversionApproach, CancellationToken, Task<ConversionPlan>>? PlanHandler { get; set; }

    public TestableMacroConverter(
        ILogger logger,
        IMacroService macroService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        IContentService contentService,
        IFileService fileService,
        IShortStringHelper shortStringHelper,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider)
        : base(logger, macroService, contentTypeService, dataTypeService, contentService,
              fileService, shortStringHelper, historyService, scopeProvider)
    {
    }

    public override string ConverterName => "Test Macro Converter";
    public override string Description => "A test macro converter for unit testing.";
    public override string TargetShapeAlias => "richTextBlock";

    public override Task<ConversionPlan> ComputePlanAsync(
        ConversionApproach approach, CancellationToken cancellationToken = default)
    {
        return PlanHandler != null
            ? PlanHandler(approach, cancellationToken)
            : Task.FromResult(new ConversionPlan { Approach = approach, ComputedAt = DateTime.UtcNow });
    }

    protected override Task ExecuteCoreAsync(
        ConversionOptions options, ConversionResult result,
        IProgress<ConversionProgress>? progress, CancellationToken cancellationToken)
    {
        return CoreHandler != null
            ? CoreHandler(options, result, progress, cancellationToken)
            : Task.CompletedTask;
    }
}

[TestClass]
public class BaseMacroConverterTests
{
    private Mock<ILogger> _loggerMock = null!;
    private Mock<IMacroService> _macroServiceMock = null!;
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IFileService> _fileServiceMock = null!;
    private Mock<IShortStringHelper> _shortStringHelperMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IScopeProvider> _scopeProviderMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _macroServiceMock = new Mock<IMacroService>();
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _fileServiceMock = new Mock<IFileService>();
        _shortStringHelperMock = new Mock<IShortStringHelper>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _scopeProviderMock = new Mock<IScopeProvider>();
    }

    private TestableMacroConverter CreateConverter()
    {
        return new TestableMacroConverter(
            _loggerMock.Object,
            _macroServiceMock.Object,
            _contentTypeServiceMock.Object,
            _dataTypeServiceMock.Object,
            _contentServiceMock.Object,
            _fileServiceMock.Object,
            _shortStringHelperMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableMacroConverter(
            null!, _macroServiceMock.Object, _contentTypeServiceMock.Object,
            _dataTypeServiceMock.Object, _contentServiceMock.Object, _fileServiceMock.Object,
            _shortStringHelperMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullMacroService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableMacroConverter(
            _loggerMock.Object, null!, _contentTypeServiceMock.Object,
            _dataTypeServiceMock.Object, _contentServiceMock.Object, _fileServiceMock.Object,
            _shortStringHelperMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullFileService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableMacroConverter(
            _loggerMock.Object, _macroServiceMock.Object, _contentTypeServiceMock.Object,
            _dataTypeServiceMock.Object, _contentServiceMock.Object, null!,
            _shortStringHelperMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void DefaultMetadata_UsesMacroCategoryAndIcon()
    {
        var converter = CreateConverter();
        ILegacyFeatureConverter asBase = converter;

        Assert.AreEqual("Macro", asBase.Category);
        Assert.AreEqual("icon-code", asBase.Icon);
        Assert.AreEqual("Test Macro Converter", asBase.ShortName); // defaults to ConverterName
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_StartsAndCompletesHistoryTracking()
    {
        var converter = CreateConverter();
        var options = new ConversionOptions
        {
            IsTestRun = true,
            PerformingUserKey = Guid.NewGuid(),
            SelectedMacroKeys = new[] { Guid.NewGuid() }
        };

        await converter.ExecuteConversionAsync(options);

        _historyServiceMock.Verify(x => x.StartConversionAsync(
            It.IsAny<Guid>(), "Test Macro Converter", true,
            options.SelectedMacroKeys, options.PerformingUserKey,
            It.IsAny<CancellationToken>()), Times.Once);

        _historyServiceMock.Verify(x => x.CompleteConversionAsync(
            It.IsAny<Guid>(), It.IsAny<ConversionResult>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_PassesOptionsToExecuteCore()
    {
        var converter = CreateConverter();
        ConversionOptions? captured = null;
        converter.CoreHandler = (opts, _, _, _) =>
        {
            captured = opts;
            return Task.CompletedTask;
        };

        var options = new ConversionOptions
        {
            IsTestRun = true,
            GenerateStubPartialViews = false
        };

        await converter.ExecuteConversionAsync(options);

        Assert.IsNotNull(captured);
        Assert.IsTrue(captured!.IsTestRun);
        Assert.IsFalse(captured.GenerateStubPartialViews);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_NoFailures_StatusCompleted()
    {
        var converter = CreateConverter();
        converter.CoreHandler = (_, result, _, _) =>
        {
            result.ContentNodes.Add(new ContentConversionInfo { Id = 1, Success = true });
            result.ContentNodes.Add(new ContentConversionInfo { Id = 2, Success = true });
            return Task.CompletedTask;
        };

        var result = await converter.ExecuteConversionAsync(new ConversionOptions());

        Assert.AreEqual(ConversionStatus.Completed, result.Status);
        Assert.IsNotNull(result.CompletedAt);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WithFailuresAndSuccesses_StatusCompletedWithErrors()
    {
        var converter = CreateConverter();
        converter.CoreHandler = (_, result, _, _) =>
        {
            result.ContentNodes.Add(new ContentConversionInfo { Id = 1, Success = true });
            // Failure: not successful and not skipped
            result.ContentNodes.Add(new ContentConversionInfo
            {
                Id = 2, Success = false, Skipped = false, ErrorMessage = "boom"
            });
            return Task.CompletedTask;
        };

        var result = await converter.ExecuteConversionAsync(new ConversionOptions());

        Assert.AreEqual(ConversionStatus.CompletedWithErrors, result.Status);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_OnlyFailures_StatusFailed()
    {
        var converter = CreateConverter();
        converter.CoreHandler = (_, result, _, _) =>
        {
            result.ContentNodes.Add(new ContentConversionInfo
            {
                Id = 1, Success = false, Skipped = false, ErrorMessage = "boom"
            });
            result.ContentNodes.Add(new ContentConversionInfo
            {
                Id = 2, Success = false, Skipped = false, ErrorMessage = "bang"
            });
            return Task.CompletedTask;
        };

        var result = await converter.ExecuteConversionAsync(new ConversionOptions());

        Assert.AreEqual(ConversionStatus.Failed, result.Status);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WhenCancelled_StatusCancelled()
    {
        var converter = CreateConverter();
        converter.CoreHandler = (_, _, _, _) => throw new OperationCanceledException();

        var result = await converter.ExecuteConversionAsync(new ConversionOptions());

        Assert.AreEqual(ConversionStatus.Cancelled, result.Status);
        Assert.IsNotNull(result.CompletedAt);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WhenExceptionThrown_StatusFailedWithMessage()
    {
        var converter = CreateConverter();
        converter.CoreHandler = (_, _, _, _) => throw new InvalidOperationException("Boom");

        var result = await converter.ExecuteConversionAsync(new ConversionOptions());

        Assert.AreEqual(ConversionStatus.Failed, result.Status);
        Assert.AreEqual("Boom", result.ErrorMessage);
        Assert.IsNotNull(result.StackTrace);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_SetsIsTestRunOnResult()
    {
        var converter = CreateConverter();
        var options = new ConversionOptions { IsTestRun = true };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.IsTrue(result.IsTestRun);
    }

    [TestMethod]
    public async Task GetAffectedUnitCountAsync_DefaultsToPlanMacroCount()
    {
        var converter = CreateConverter();
        converter.PlanHandler = (_, _) => Task.FromResult(new ConversionPlan
        {
            Macros = new List<ConversionPlanMacro>
            {
                new() { Alias = "a" },
                new() { Alias = "b" },
                new() { Alias = "c" }
            }
        });

        ILegacyFeatureConverter asBase = converter;
        var count = await asBase.GetAffectedUnitCountAsync();

        Assert.AreEqual(3, count);
    }
}
