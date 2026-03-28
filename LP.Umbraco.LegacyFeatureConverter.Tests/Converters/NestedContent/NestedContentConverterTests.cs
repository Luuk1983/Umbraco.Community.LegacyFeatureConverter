using LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Converters.NestedContent;

[TestClass]
public class NestedContentConverterTests
{
    private Mock<ILogger<NestedContentConverter>> _loggerMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IScopeProvider> _scopeProviderMock = null!;
    private Mock<IDataValueEditorFactory> _dataValueEditorFactoryMock = null!;
    private Mock<PropertyEditorCollection> _propertyEditorCollectionMock = null!;
    private Mock<IConfigurationEditorJsonSerializer> _configSerializerMock = null!;

    private NestedContentConverter _converter = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<NestedContentConverter>>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _scopeProviderMock = new Mock<IScopeProvider>();
        _dataValueEditorFactoryMock = new Mock<IDataValueEditorFactory>();
        _propertyEditorCollectionMock = new Mock<PropertyEditorCollection>(() =>
            new PropertyEditorCollection(new DataEditorCollection(() => Enumerable.Empty<IDataEditor>())));
        _configSerializerMock = new Mock<IConfigurationEditorJsonSerializer>();

        _converter = new NestedContentConverter(
            _loggerMock.Object,
            _dataTypeServiceMock.Object,
            _contentTypeServiceMock.Object,
            _contentServiceMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object,
            _dataValueEditorFactoryMock.Object,
            _propertyEditorCollectionMock.Object,
            _configSerializerMock.Object);
    }

    /// <summary>
    /// Sets up the content type service to resolve an alias to a mock content type with a given key.
    /// </summary>
    private void SetupContentType(string alias, Guid key)
    {
        var contentType = new Mock<IContentType>();
        contentType.Setup(c => c.Key).Returns(key);
        contentType.Setup(c => c.Alias).Returns(alias);
        _contentTypeServiceMock.Setup(s => s.Get(alias)).Returns(contentType.Object);
    }

    [TestMethod]
    public void Properties_ReturnExpectedValues()
    {
        Assert.AreEqual("Nested Content to Block List", _converter.ConverterName);
        Assert.AreEqual("Umbraco.BlockList", _converter.TargetPropertyEditorAlias);
        CollectionAssert.AreEqual(new[] { "Umbraco.NestedContent" }, _converter.SourcePropertyEditorAliases);
    }

    // ===== ConvertPropertyValueAsync tests =====

    [TestMethod]
    public async Task ConvertPropertyValue_WithNull_ReturnsNull()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("test");

        var result = await InvokeConvertPropertyValue(null!, property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithEmptyString_ReturnsNull()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("test");

        var result = await InvokeConvertPropertyValue("", property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithEmptyArray_ReturnsNull()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("test");

        var result = await InvokeConvertPropertyValue("[]", property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithInvalidJson_ReturnsNull()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("test");

        var result = await InvokeConvertPropertyValue("not json", property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_SimpleItem_ProducesValidBlockList()
    {
        var contentTypeKey = Guid.NewGuid();
        SetupContentType("textBlock", contentTypeKey);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        var ncJson = @"[{""ncContentTypeAlias"":""textBlock"",""title"":""Hello World"",""name"":""Item 1"",""key"":""abc""}]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var blockList = JObject.Parse(resultString);

        // Verify structure
        Assert.IsNotNull(blockList["layout"]);
        Assert.IsNotNull(blockList["contentData"]);
        Assert.IsNotNull(blockList["settingsData"]);

        // Verify content data
        var contentData = blockList["contentData"] as JArray;
        Assert.AreEqual(1, contentData!.Count);
        Assert.AreEqual(contentTypeKey.ToString(), contentData[0]["contentTypeKey"]!.ToString());
        Assert.AreEqual("Hello World", contentData[0]["title"]!.ToString());

        // NC metadata fields should be stripped
        Assert.IsNull(contentData[0]["ncContentTypeAlias"]);
        Assert.IsNull(contentData[0]["name"]);
        Assert.IsNull(contentData[0]["key"]);

        // Verify layout has a contentUdi
        var layoutItems = blockList["layout"]!["Umbraco.BlockList"] as JArray;
        Assert.AreEqual(1, layoutItems!.Count);
        Assert.IsNotNull(layoutItems[0]["contentUdi"]);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_MultipleItems_AllConverted()
    {
        SetupContentType("textBlock", Guid.NewGuid());
        SetupContentType("imageBlock", Guid.NewGuid());

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        var ncJson = @"[
            {""ncContentTypeAlias"":""textBlock"",""text"":""One""},
            {""ncContentTypeAlias"":""imageBlock"",""src"":""/image.jpg""},
            {""ncContentTypeAlias"":""textBlock"",""text"":""Two""}
        ]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;
        var layoutItems = blockList["layout"]!["Umbraco.BlockList"] as JArray;

        Assert.AreEqual(3, contentData!.Count);
        Assert.AreEqual(3, layoutItems!.Count);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_UnknownContentType_SkipsItem()
    {
        SetupContentType("knownBlock", Guid.NewGuid());
        // "unknownBlock" is NOT set up — will return null from contentTypeService

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        var ncJson = @"[
            {""ncContentTypeAlias"":""knownBlock"",""text"":""Hello""},
            {""ncContentTypeAlias"":""unknownBlock"",""text"":""World""}
        ]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;

        // Only the known block should be in the output
        Assert.AreEqual(1, contentData!.Count);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_PreservesComplexPropertyValues()
    {
        SetupContentType("richBlock", Guid.NewGuid());

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        // Property value contains HTML (plain string) and a JSON object
        var ncJson = @"[{""ncContentTypeAlias"":""richBlock"",""body"":""<p>Hello</p>"",""settings"":{""color"":""red""}}]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;

        Assert.AreEqual("<p>Hello</p>", contentData![0]["body"]!.ToString());
        // JSON object should be preserved as serialized string
        Assert.AreEqual(@"{""color"":""red""}", contentData[0]["settings"]!.ToString());
    }

    [TestMethod]
    public async Task ConvertPropertyValue_NestedNC_ConvertedRecursively()
    {
        var outerKey = Guid.NewGuid();
        var innerKey = Guid.NewGuid();
        SetupContentType("outerBlock", outerKey);
        SetupContentType("innerBlock", innerKey);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        // Outer NC with a property that is itself NC
        var ncJson = @"[{
            ""ncContentTypeAlias"":""outerBlock"",
            ""title"":""Outer"",
            ""nestedItems"":[{""ncContentTypeAlias"":""innerBlock"",""text"":""Inner""}]
        }]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;

        Assert.AreEqual(1, contentData!.Count);
        Assert.AreEqual("Outer", contentData[0]["title"]!.ToString());

        // The nested items property should now be a Block List JSON string
        var nestedBLJson = contentData[0]["nestedItems"]!.ToString();
        var nestedBL = JObject.Parse(nestedBLJson);
        var nestedContentData = nestedBL["contentData"] as JArray;

        Assert.AreEqual(1, nestedContentData!.Count);
        Assert.AreEqual(innerKey.ToString(), nestedContentData[0]["contentTypeKey"]!.ToString());
        Assert.AreEqual("Inner", nestedContentData[0]["text"]!.ToString());
    }

    [TestMethod]
    public async Task ConvertPropertyValue_NonNCJsonArray_CopiedAsIs()
    {
        SetupContentType("mediaBlock", Guid.NewGuid());

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        // Media picker value is a JSON array but NOT nested content (no ncContentTypeAlias)
        var ncJson = @"[{
            ""ncContentTypeAlias"":""mediaBlock"",
            ""images"":[{""mediaKey"":""abc-123"",""crops"":[]}]
        }]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;

        // The images property should be copied as-is (not treated as nested NC)
        var images = contentData![0]["images"]!.ToString();
        Assert.IsTrue(images.Contains("mediaKey"));
    }

    [TestMethod]
    public async Task ConvertPropertyValue_NullPropertyValues_Skipped()
    {
        SetupContentType("testBlock", Guid.NewGuid());

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        var ncJson = @"[{""ncContentTypeAlias"":""testBlock"",""title"":""Hello"",""nullProp"":null}]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var blockList = JObject.Parse(result.ToString()!);
        var contentData = blockList["contentData"] as JArray;

        Assert.AreEqual("Hello", contentData![0]["title"]!.ToString());
        // Null properties should be skipped (not present in output)
        Assert.IsNull(contentData[0]["nullProp"]);
    }

    /// <summary>
    /// Helper to invoke the protected ConvertPropertyValueAsync via the public interface.
    /// </summary>
    private async Task<object?> InvokeConvertPropertyValue(object sourceValue, IProperty property)
    {
        // Use the public ExecuteConversionAsync with a setup that only converts one item,
        // or use reflection. For simplicity, test via the converter's internal method chain
        // by calling it through a wrapper. We'll use a helper approach here.

        // Actually, since ConvertPropertyValueAsync is protected, we test it indirectly
        // through the full execution flow or through a testable subclass.
        // Let's create a subclass approach that exposes the method:
        var testable = new TestableNestedContentConverter(
            _loggerMock.Object,
            _dataTypeServiceMock.Object,
            _contentTypeServiceMock.Object,
            _contentServiceMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object,
            _dataValueEditorFactoryMock.Object,
            _propertyEditorCollectionMock.Object,
            _configSerializerMock.Object);

        return await testable.TestConvertPropertyValueAsync(sourceValue, property);
    }
}

/// <summary>
/// Testable subclass that exposes the protected ConvertPropertyValueAsync method.
/// </summary>
internal class TestableNestedContentConverter : NestedContentConverter
{
    public TestableNestedContentConverter(
        ILogger<NestedContentConverter> logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider,
        IDataValueEditorFactory dataValueEditorFactory,
        PropertyEditorCollection propertyEditorCollection,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        : base(logger, dataTypeService, contentTypeService, contentService, historyService,
            scopeProvider, dataValueEditorFactory, propertyEditorCollection, configurationEditorJsonSerializer)
    {
    }

    /// <summary>
    /// Exposes the protected ConvertPropertyValueAsync for testing.
    /// </summary>
    public Task<object?> TestConvertPropertyValueAsync(object sourceValue, IProperty property)
    {
        return ConvertPropertyValueAsync(sourceValue, property);
    }
}
