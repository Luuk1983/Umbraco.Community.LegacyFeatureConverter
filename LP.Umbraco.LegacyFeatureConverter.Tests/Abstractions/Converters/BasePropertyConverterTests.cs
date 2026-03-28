using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Abstractions.Converters;

/// <summary>
/// A concrete implementation of BasePropertyConverter for testing purposes.
/// </summary>
internal class TestableConverter : BasePropertyConverter
{
    /// <summary>
    /// Optional delegate to customize CreateTargetDataTypeAsync behavior in tests.
    /// </summary>
    public Func<IDataType, Task<IDataType?>>? CreateTargetDataTypeHandler { get; set; }

    /// <summary>
    /// Optional delegate to customize ConvertPropertyValueAsync behavior in tests.
    /// </summary>
    public Func<object, IProperty, Task<object?>>? ConvertPropertyValueHandler { get; set; }

    public TestableConverter(
        ILogger logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider)
        : base(logger, dataTypeService, contentTypeService, contentService, historyService, scopeProvider)
    {
    }

    public override string ConverterName => "Test Converter";
    public override string[] SourcePropertyEditorAliases => new[] { "Umbraco.TestEditor" };
    public override string TargetPropertyEditorAlias => "Umbraco.NewTestEditor";
    public override string Description => "A test converter for unit testing.";

    protected override Task<IDataType?> CreateTargetDataTypeAsync(IDataType sourceDataType)
    {
        if (CreateTargetDataTypeHandler != null)
            return CreateTargetDataTypeHandler(sourceDataType);

        return Task.FromResult<IDataType?>(null);
    }

    protected override Task<object?> ConvertPropertyValueAsync(object sourceValue, IProperty property)
    {
        if (ConvertPropertyValueHandler != null)
            return ConvertPropertyValueHandler(sourceValue, property);

        return Task.FromResult<object?>(null);
    }
}

[TestClass]
public class BasePropertyConverterTests
{
    private Mock<ILogger> _loggerMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IScopeProvider> _scopeProviderMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _scopeProviderMock = new Mock<IScopeProvider>();
    }

    /// <summary>
    /// Creates a TestableConverter with all standard mocks.
    /// </summary>
    private TestableConverter CreateConverter()
    {
        return new TestableConverter(
            _loggerMock.Object,
            _dataTypeServiceMock.Object,
            _contentTypeServiceMock.Object,
            _contentServiceMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            null!, _dataTypeServiceMock.Object, _contentTypeServiceMock.Object,
            _contentServiceMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullDataTypeService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            _loggerMock.Object, null!, _contentTypeServiceMock.Object,
            _contentServiceMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullContentTypeService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            _loggerMock.Object, _dataTypeServiceMock.Object, null!,
            _contentServiceMock.Object, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullContentService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            _loggerMock.Object, _dataTypeServiceMock.Object, _contentTypeServiceMock.Object,
            null!, _historyServiceMock.Object, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullHistoryService_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            _loggerMock.Object, _dataTypeServiceMock.Object, _contentTypeServiceMock.Object,
            _contentServiceMock.Object, null!, _scopeProviderMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullScopeProvider_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new TestableConverter(
            _loggerMock.Object, _dataTypeServiceMock.Object, _contentTypeServiceMock.Object,
            _contentServiceMock.Object, _historyServiceMock.Object, null!));
    }

    [TestMethod]
    public void Properties_ReturnExpectedValues()
    {
        var converter = CreateConverter();

        Assert.AreEqual("Test Converter", converter.ConverterName);
        Assert.AreEqual("Umbraco.NewTestEditor", converter.TargetPropertyEditorAlias);
        Assert.AreEqual("A test converter for unit testing.", converter.Description);
        CollectionAssert.AreEqual(new[] { "Umbraco.TestEditor" }, converter.SourcePropertyEditorAliases);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WithNoDocumentTypes_CompletesSuccessfully()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(Enumerable.Empty<IContentType>());

        var options = new ConversionOptions
        {
            IsTestRun = false,
            PerformingUserKey = Guid.NewGuid()
        };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(ConversionStatus.Completed, result.Status);
        Assert.AreEqual(0, result.TotalItems);
        Assert.IsNotNull(result.CompletedAt);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_StartsAndCompletesHistoryTracking()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(Enumerable.Empty<IContentType>());

        var options = new ConversionOptions
        {
            IsTestRun = true,
            PerformingUserKey = Guid.NewGuid()
        };

        await converter.ExecuteConversionAsync(options);

        _historyServiceMock.Verify(x => x.StartConversionAsync(
            It.IsAny<Guid>(), "Test Converter", true,
            It.IsAny<Guid[]?>(), options.PerformingUserKey,
            It.IsAny<CancellationToken>()), Times.Once);

        _historyServiceMock.Verify(x => x.CompleteConversionAsync(
            It.IsAny<Guid>(), It.IsAny<ConversionResult>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WhenCancelled_SetsStatusToCancelled()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Callback(() => throw new OperationCanceledException())
            .Returns(Enumerable.Empty<IContentType>());

        var options = new ConversionOptions();
        var cts = new CancellationTokenSource();

        var result = await converter.ExecuteConversionAsync(options, cancellationToken: cts.Token);

        Assert.AreEqual(ConversionStatus.Cancelled, result.Status);
        Assert.IsNotNull(result.CompletedAt);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_WhenExceptionThrown_SetsStatusToFailed()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Throws(new InvalidOperationException("Test error"));

        var options = new ConversionOptions();

        var result = await converter.ExecuteConversionAsync(options);

        Assert.AreEqual(ConversionStatus.Failed, result.Status);
        Assert.AreEqual("Test error", result.ErrorMessage);
        Assert.IsNotNull(result.StackTrace);
        Assert.IsNotNull(result.CompletedAt);
    }

    [TestMethod]
    public async Task ExecuteConversionAsync_SetsIsTestRunOnResult()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(Enumerable.Empty<IContentType>());

        var options = new ConversionOptions { IsTestRun = true };

        var result = await converter.ExecuteConversionAsync(options);

        Assert.IsTrue(result.IsTestRun);
    }

    [TestMethod]
    public async Task GetAffectedDocumentTypesCountAsync_WithNoDocTypes_ReturnsZero()
    {
        var converter = CreateConverter();

        _contentTypeServiceMock.Setup(x => x.GetAll())
            .Returns(Enumerable.Empty<IContentType>());

        var count = await converter.GetAffectedDocumentTypesCountAsync();

        Assert.AreEqual(0, count);
    }
}
