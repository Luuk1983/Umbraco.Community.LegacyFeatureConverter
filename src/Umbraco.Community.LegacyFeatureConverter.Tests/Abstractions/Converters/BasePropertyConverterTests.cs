using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Converters;

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

    /// <summary>
    /// Exposes the protected ConvertContentForDocTypeAsync for testing.
    /// </summary>
    public Task<int> TestConvertContentForDocTypeAsync(
        ConversionResult result,
        IContentType docType,
        HashSet<string> aliasesToConvert,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        return ConvertContentForDocTypeAsync(result, docType, aliasesToConvert, options, null, 0, 0, cancellationToken);
    }

    /// <summary>
    /// Exposes the protected ConvertContentDataAsync for testing.
    /// </summary>
    public Task TestConvertContentDataAsync(
        ConversionResult result,
        List<IContentType> documentTypes,
        List<IContentType>? contentOnlyDocTypes,
        Dictionary<int, HashSet<string>> propertyAliasesToConvert,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        return ConvertContentDataAsync(result, documentTypes, contentOnlyDocTypes,
            propertyAliasesToConvert, options, null, cancellationToken);
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

    // ===== Culture-variant content handling tests =====

    /// <summary>
    /// Creates a mock IProperty for an invariant property (no culture variation).
    /// </summary>
    private static Mock<IProperty> CreateInvariantPropertyMock(string alias, object? value, object? convertedValue)
    {
        var propertyTypeMock = new Mock<IPropertyType>();
        propertyTypeMock.Setup(pt => pt.Alias).Returns(alias);
        propertyTypeMock.Setup(pt => pt.Variations).Returns(ContentVariation.Nothing);

        var propertyMock = new Mock<IProperty>();
        propertyMock.Setup(p => p.Alias).Returns(alias);
        propertyMock.Setup(p => p.PropertyType).Returns(propertyTypeMock.Object);
        propertyMock.Setup(p => p.GetValue(null, null)).Returns(value);

        return propertyMock;
    }

    /// <summary>
    /// Creates a mock IProperty for a culture-variant property with the given cultures and values.
    /// </summary>
    private static Mock<IProperty> CreateCultureVariantPropertyMock(
        string alias,
        IReadOnlyDictionary<string, object?> cultureValues)
    {
        var propertyTypeMock = new Mock<IPropertyType>();
        propertyTypeMock.Setup(pt => pt.Alias).Returns(alias);
        propertyTypeMock.Setup(pt => pt.Variations).Returns(ContentVariation.Culture);

        var propertyValueMocks = cultureValues.Keys.Select(culture =>
        {
            var pv = new Mock<IPropertyValue>();
            pv.Setup(v => v.Culture).Returns(culture);
            return pv.Object;
        }).ToList();

        var propertyMock = new Mock<IProperty>();
        propertyMock.Setup(p => p.Alias).Returns(alias);
        propertyMock.Setup(p => p.PropertyType).Returns(propertyTypeMock.Object);
        propertyMock.Setup(p => p.Values).Returns(propertyValueMocks);
        foreach (var (culture, value) in cultureValues)
        {
            propertyMock.Setup(p => p.GetValue(culture, null)).Returns(value);
        }

        return propertyMock;
    }

    /// <summary>
    /// Creates a minimal mock IContent node with the given properties.
    /// </summary>
    private static Mock<IContent> CreateContentMock(int id, params IProperty[] properties)
    {
        var propertiesMock = new Mock<IPropertyCollection>();
        var propertyList = properties.ToList();
        propertiesMock.As<IEnumerable<IProperty>>()
            .Setup(x => x.GetEnumerator())
            .Returns(() => propertyList.GetEnumerator());

        var contentMock = new Mock<IContent>();
        contentMock.Setup(c => c.Id).Returns(id);
        contentMock.Setup(c => c.Key).Returns(Guid.NewGuid());
        contentMock.Setup(c => c.Name).Returns($"Content {id}");
        contentMock.Setup(c => c.Properties).Returns(propertiesMock.Object);

        return contentMock;
    }

    /// <summary>
    /// Creates a minimal mock IContentType with the given source properties.
    /// </summary>
    private Mock<IContentType> CreateDocTypeMock(int id, params Mock<IProperty>[] properties)
    {
        var propertyTypes = properties.Select(p =>
        {
            var ptMock = new Mock<IPropertyType>();
            ptMock.Setup(pt => pt.Alias).Returns(p.Object.Alias);
            ptMock.Setup(pt => pt.PropertyEditorAlias).Returns("Umbraco.TestEditor");
            ptMock.Setup(pt => pt.DataTypeId).Returns(99);
            return ptMock.Object;
        }).ToList();

        var docTypeMock = new Mock<IContentType>();
        docTypeMock.Setup(dt => dt.Id).Returns(id);
        docTypeMock.Setup(dt => dt.Key).Returns(Guid.NewGuid());
        docTypeMock.Setup(dt => dt.Name).Returns($"DocType {id}");
        docTypeMock.Setup(dt => dt.Alias).Returns($"docType{id}");
        docTypeMock.Setup(dt => dt.PropertyTypes).Returns(propertyTypes);
        docTypeMock.Setup(dt => dt.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());
        docTypeMock.Setup(dt => dt.ContentTypeComposition).Returns(Enumerable.Empty<IContentTypeComposition>());

        return docTypeMock;
    }

    [TestMethod]
    public async Task ConvertContentForDocType_InvariantProperty_CallsGetValueWithoutCulture()
    {
        // Arrange: single invariant property with a value
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var propertyMock = CreateInvariantPropertyMock("myProp", "original", "converted:original");
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: invariant GetValue called, converted value set without culture
        propertyMock.Verify(p => p.GetValue(null, null), Times.Once);
        propertyMock.Verify(p => p.SetValue("converted:original", null, null), Times.Once);
        // Verify that no culture-specific SetValue was called
        propertyMock.Verify(p => p.SetValue(It.IsAny<object?>(), It.Is<string?>(c => c != null), It.IsAny<string?>()), Times.Never);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_CultureVariantProperty_ConvertsAllCultures()
    {
        // Arrange: property with values in two cultures
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var cultureValues = new Dictionary<string, object?>
        {
            { "en-us", "english-value" },
            { "nl", "dutch-value" }
        };
        var propertyMock = CreateCultureVariantPropertyMock("myProp", cultureValues);
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: both cultures converted
        propertyMock.Verify(p => p.SetValue("converted:english-value", "en-us", null), Times.Once);
        propertyMock.Verify(p => p.SetValue("converted:dutch-value", "nl", null), Times.Once);
        // Invariant value must NOT be touched
        propertyMock.Verify(p => p.SetValue(It.IsAny<object?>(), (string?)null, It.IsAny<string?>()), Times.Never);
        // The content node was counted as successful
        Assert.HasCount(1, result.ContentNodes);
        Assert.IsTrue(result.ContentNodes[0].Success);
        Assert.AreEqual(2, result.ContentNodes[0].PropertiesConverted);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_CultureVariantProperty_NullCultureValue_SkipsThatCulture()
    {
        // Arrange: English has a value, Dutch returns null (no content in that culture)
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var cultureValues = new Dictionary<string, object?>
        {
            { "en-us", "english-value" },
            { "nl", null }   // null → should be skipped
        };
        var propertyMock = CreateCultureVariantPropertyMock("myProp", cultureValues);
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: only English was converted; Dutch was skipped
        propertyMock.Verify(p => p.SetValue("converted:english-value", "en-us", null), Times.Once);
        propertyMock.Verify(p => p.SetValue(It.IsAny<object?>(), "nl", null), Times.Never);
        Assert.AreEqual(1, result.ContentNodes[0].PropertiesConverted);
    }

    // ===== PublishAfterConversion option tests =====

    [TestMethod]
    public async Task ConvertContentForDocType_WhenNotTestRun_AndPublishFalse_CallsSave()
    {
        // Arrange
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var propertyMock = CreateInvariantPropertyMock("myProp", "original", "converted:original");
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = false, PublishAfterConversion = false };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: Save called, NOT SaveAndPublish
        _contentServiceMock.Verify(s => s.Save(contentMock.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Once);
        _contentServiceMock.Verify(s => s.SaveAndPublish(contentMock.Object, It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_WhenNotTestRun_AndPublishTrue_CallsSaveAndPublish()
    {
        // Arrange
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var propertyMock = CreateInvariantPropertyMock("myProp", "original", "converted:original");
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = false, PublishAfterConversion = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: SaveAndPublish called, NOT Save
        _contentServiceMock.Verify(s => s.SaveAndPublish(contentMock.Object, It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        _contentServiceMock.Verify(s => s.Save(contentMock.Object, It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_WhenTestRun_NeitherSaveNorPublishCalled()
    {
        // Arrange
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var propertyMock = CreateInvariantPropertyMock("myProp", "original", "converted:original");
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        // Test run with publish option set to true — publish must still not happen
        var options = new ConversionOptions { IsTestRun = true, PublishAfterConversion = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: neither Save nor SaveAndPublish called during test run
        _contentServiceMock.Verify(s => s.Save(It.IsAny<IContent>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
        _contentServiceMock.Verify(s => s.SaveAndPublish(It.IsAny<IContent>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_WhenNotModified_NeitherSaveNorPublishCalled()
    {
        // Arrange: converter returns null (no conversion needed)
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) => Task.FromResult<object?>(null);

        var propertyMock = CreateInvariantPropertyMock("myProp", "already-converted", null);
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = false, PublishAfterConversion = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: content skipped, not saved
        _contentServiceMock.Verify(s => s.Save(It.IsAny<IContent>(), It.IsAny<int?>(), It.IsAny<ContentScheduleCollection?>()), Times.Never);
        _contentServiceMock.Verify(s => s.SaveAndPublish(It.IsAny<IContent>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.IsTrue(result.ContentNodes[0].Skipped);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_CultureVariantProperty_AlreadyConverted_SkipsCulture()
    {
        // Arrange: converter returns null for Dutch (already in target format)
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
        {
            if (value.ToString() == "already-block-list-nl") return Task.FromResult<object?>(null);
            return Task.FromResult<object?>("converted:" + value);
        };

        var cultureValues = new Dictionary<string, object?>
        {
            { "en-us", "english-old" },
            { "nl", "already-block-list-nl" }
        };
        var propertyMock = CreateCultureVariantPropertyMock("myProp", cultureValues);
        var contentMock = CreateContentMock(100, propertyMock.Object);
        var docTypeMock = CreateDocTypeMock(1, propertyMock);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "myProp" };
        var options = new ConversionOptions { IsTestRun = true };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: English set, Dutch skipped
        propertyMock.Verify(p => p.SetValue("converted:english-old", "en-us", null), Times.Once);
        propertyMock.Verify(p => p.SetValue(It.IsAny<object?>(), "nl", null), Times.Never);
        Assert.AreEqual(1, result.ContentNodes[0].PropertiesConverted);
    }

    // ===== Logging verbosity tests =====

    [TestMethod]
    public async Task ConvertContentForDocType_WithMultiplePropertiesConverted_LogsOncePerContentNode()
    {
        // Arrange: two properties on the same content node, both converted.
        // After the change, only ONE LogEntryAsync call with itemType "Content" should be made
        // (one per content node, not one per property).
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) =>
            Task.FromResult<object?>("converted:" + value);

        var prop1 = CreateInvariantPropertyMock("prop1", "value1", "converted:value1");
        var prop2 = CreateInvariantPropertyMock("prop2", "value2", "converted:value2");
        var contentMock = CreateContentMock(100, prop1.Object, prop2.Object);
        var docTypeMock = CreateDocTypeMock(1, prop1, prop2);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "prop1", "prop2" };
        var options = new ConversionOptions { IsTestRun = false };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: exactly ONE Info-level "Content" log entry, despite 2 properties being converted
        _historyServiceMock.Verify(
            x => x.LogEntryAsync(
                result.ConversionId, LogLevel.Information, "Content",
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.AreEqual(2, result.ContentNodes[0].PropertiesConverted);
    }

    // ===== Phase 4 count-loop regression =====

    [TestMethod]
    public async Task ConvertContentDataAsync_CountLoop_UsesScalarCountQuery()
    {
        // Regression: ConvertContentDataAsync used to call GetPagedOfType with pageSize=0
        // when pre-computing the total content count, which Umbraco rejects with
        // ArgumentOutOfRangeException. The fix uses IContentService.Count, which runs
        // a SELECT COUNT(*) — no records fetched.
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (_, _) => Task.FromResult<object?>(null);

        var ptMock = new Mock<IPropertyType>();
        ptMock.Setup(pt => pt.Alias).Returns("myProp");
        ptMock.Setup(pt => pt.PropertyEditorAlias).Returns("Umbraco.NewTestEditor"); // already on TARGET
        ptMock.Setup(pt => pt.DataTypeId).Returns(99);

        var docTypeMock = new Mock<IContentType>();
        docTypeMock.Setup(dt => dt.Id).Returns(1);
        docTypeMock.Setup(dt => dt.Key).Returns(Guid.NewGuid());
        docTypeMock.Setup(dt => dt.Name).Returns("DocType 1");
        docTypeMock.Setup(dt => dt.Alias).Returns("docType1");
        docTypeMock.Setup(dt => dt.PropertyTypes).Returns(new[] { ptMock.Object });
        docTypeMock.Setup(dt => dt.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());

        _contentServiceMock.Setup(x => x.Count("docType1")).Returns(0);

        long fetchTotal = 0;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out fetchTotal, null!))
            .Returns(Enumerable.Empty<IContent>());

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var options = new ConversionOptions { IsTestRun = true };

        // Act: simulate the contentOnly path (schema already migrated, content not)
        await converter.TestConvertContentDataAsync(
            result,
            documentTypes: new List<IContentType>(),
            contentOnlyDocTypes: new List<IContentType> { docTypeMock.Object },
            propertyAliasesToConvert: new Dictionary<int, HashSet<string>>(),
            options);

        // Assert: scalar Count was used (no records fetched for the count phase)
        _contentServiceMock.Verify(x => x.Count("docType1"), Times.Once);

        // GetPagedOfType must NOT be used as a count-only query (no pageSize <= 1 calls)
        _contentServiceMock.Verify(
            x => x.GetPagedOfType(It.IsAny<int>(), It.IsAny<long>(), It.Is<int>(p => p <= 1),
                out It.Ref<long>.IsAny,
                It.IsAny<global::Umbraco.Cms.Core.Persistence.Querying.IQuery<IContent>>()),
            Times.Never,
            "Count loop should use IContentService.Count, not a 1-record GetPagedOfType query");
    }

    [TestMethod]
    public async Task ConvertContentDataAsync_ContentOnlyDocType_DoesNotCountAsFailedWhenNoContent()
    {
        // Regression for the "14 failed but no Error rows" symptom in the uSync flow:
        // ExecuteConversionAsync seeds result.DocumentTypes with one entry per doc type from
        // BOTH the schema-update list AND the content-only list, with Success=false &&
        // Skipped=false defaults. Phase 3 only processes the schema-update list, so
        // content-only entries used to sit there with both flags false — counted as Failed
        // in FailureCount with no Error row ever written.
        //
        // After the fix, content-only entries get Skipped=true (no content) or Success=true
        // (content processed) at the end of Phase 4.
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (_, _) => Task.FromResult<object?>(null);

        var docTypeKey = Guid.NewGuid();
        var ptMock = new Mock<IPropertyType>();
        ptMock.Setup(pt => pt.Alias).Returns("myProp");
        ptMock.Setup(pt => pt.PropertyEditorAlias).Returns("Umbraco.NewTestEditor");
        ptMock.Setup(pt => pt.DataTypeId).Returns(99);

        var docTypeMock = new Mock<IContentType>();
        docTypeMock.Setup(dt => dt.Id).Returns(1);
        docTypeMock.Setup(dt => dt.Key).Returns(docTypeKey);
        docTypeMock.Setup(dt => dt.Name).Returns("DocType 1");
        docTypeMock.Setup(dt => dt.Alias).Returns("docType1");
        docTypeMock.Setup(dt => dt.PropertyTypes).Returns(new[] { ptMock.Object });
        docTypeMock.Setup(dt => dt.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());

        _contentServiceMock.Setup(x => x.Count("docType1")).Returns(0);
        long fetchTotal = 0;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out fetchTotal, null!))
            .Returns(Enumerable.Empty<IContent>());

        // Mimic ExecuteConversionAsync's seeding step for a content-only doc type
        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        result.DocumentTypes.Add(new DocumentTypeConversionInfo
        {
            Key = docTypeKey, Name = "DocType 1", Alias = "docType1"
        });

        await converter.TestConvertContentDataAsync(
            result,
            documentTypes: new List<IContentType>(),
            contentOnlyDocTypes: new List<IContentType> { docTypeMock.Object },
            propertyAliasesToConvert: new Dictionary<int, HashSet<string>>(),
            options: new ConversionOptions { IsTestRun = true });

        var dtInfo = result.DocumentTypes.Single(d => d.Key == docTypeKey);
        Assert.IsTrue(dtInfo.Skipped, "Content-only doc type with no content should be marked Skipped, not Failed.");
        Assert.IsFalse(dtInfo.Success);
        Assert.AreEqual(0, result.FailureCount, "FailureCount must not count untouched content-only doc-type placeholders as failures.");
    }

    [TestMethod]
    public async Task ConvertContentDataAsync_ContentOnlyDocType_MarksSuccessWhenContentProcessed()
    {
        // Companion to the above: when there IS content to process under a content-only
        // doc type, its DocumentTypes entry must be marked Success (not left as a ghost failure).
        var converter = CreateConverter();
        // Return a converted value so wasModified=true and the content row is saved
        converter.ConvertPropertyValueHandler = (_, _) => Task.FromResult<object?>((object?)"converted");

        var docTypeKey = Guid.NewGuid();
        var ptMock = new Mock<IPropertyType>();
        ptMock.Setup(pt => pt.Alias).Returns("myProp");
        ptMock.Setup(pt => pt.PropertyEditorAlias).Returns("Umbraco.NewTestEditor");
        ptMock.Setup(pt => pt.DataTypeId).Returns(99);
        ptMock.Setup(pt => pt.Variations).Returns(ContentVariation.Nothing);

        var docTypeMock = new Mock<IContentType>();
        docTypeMock.Setup(dt => dt.Id).Returns(1);
        docTypeMock.Setup(dt => dt.Key).Returns(docTypeKey);
        docTypeMock.Setup(dt => dt.Name).Returns("DocType 1");
        docTypeMock.Setup(dt => dt.Alias).Returns("docType1");
        docTypeMock.Setup(dt => dt.PropertyTypes).Returns(new[] { ptMock.Object });
        docTypeMock.Setup(dt => dt.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());

        var prop = CreateInvariantPropertyMock("myProp", "[]", null);
        var contentMock = CreateContentMock(100, prop.Object);

        _contentServiceMock.Setup(x => x.Count("docType1")).Returns(1);
        long fetchTotal = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out fetchTotal, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        result.DocumentTypes.Add(new DocumentTypeConversionInfo
        {
            Key = docTypeKey, Name = "DocType 1", Alias = "docType1"
        });

        await converter.TestConvertContentDataAsync(
            result,
            documentTypes: new List<IContentType>(),
            contentOnlyDocTypes: new List<IContentType> { docTypeMock.Object },
            propertyAliasesToConvert: new Dictionary<int, HashSet<string>>(),
            options: new ConversionOptions { IsTestRun = true });

        var dtInfo = result.DocumentTypes.Single(d => d.Key == docTypeKey);
        Assert.IsTrue(dtInfo.Success, "Content-only doc type whose content was processed should be marked Success.");
        Assert.IsFalse(dtInfo.Skipped);
        Assert.AreEqual(0, result.FailureCount);
    }

    [TestMethod]
    public async Task ConvertContentForDocType_WhenContentSkipped_NoContentLogEntry()
    {
        // Arrange: converter returns null for all properties — content is not modified.
        var converter = CreateConverter();
        converter.ConvertPropertyValueHandler = (value, prop) => Task.FromResult<object?>(null);

        var prop1 = CreateInvariantPropertyMock("prop1", "already-converted", null);
        var contentMock = CreateContentMock(100, prop1.Object);
        var docTypeMock = CreateDocTypeMock(1, prop1);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(x => x.GetPagedOfType(1, 0, int.MaxValue, out totalRecords, null!))
            .Returns(new[] { contentMock.Object });

        var result = new ConversionResult { ConversionId = Guid.NewGuid() };
        var aliases = new HashSet<string> { "prop1" };
        var options = new ConversionOptions { IsTestRun = false };

        // Act
        await converter.TestConvertContentForDocTypeAsync(result, docTypeMock.Object, aliases, options);

        // Assert: no Info-level "Content" log entry for skipped content
        _historyServiceMock.Verify(
            x => x.LogEntryAsync(
                result.ConversionId, LogLevel.Information, "Content",
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.IsTrue(result.ContentNodes[0].Skipped);
    }
}
