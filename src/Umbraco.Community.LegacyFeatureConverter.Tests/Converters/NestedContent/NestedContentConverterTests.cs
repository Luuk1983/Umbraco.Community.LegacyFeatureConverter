using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Converters.NestedContent;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Converters.NestedContent;

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
    public async Task ConvertPropertyValue_WithInvalidJson_Throws()
    {
        // Behaviour change: previously the converter swallowed every exception and returned
        // null, which Phase 4 treats as "no change needed" and the item ends up Skipped — so
        // a corrupted stored value would silently slip past the conversion. We now throw
        // PropertyConversionException with attribution context so the outer per-content
        // catch in BasePropertyConverter writes a clear Error row.
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("test");

        var ex = await Assert.ThrowsExactlyAsync<PropertyConversionException>(
            () => InvokeConvertPropertyValue("not json", property.Object));
        Assert.AreEqual("test", ex.PropertyAlias);
        Assert.IsNull(ex.InnerKey);
        Assert.IsTrue(ex.Reason.Contains("not valid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithMissingElementType_Throws()
    {
        // The user's actual data-corruption root cause: outer NC value with one item
        // referencing an element-type alias that doesn't exist. Previously we returned
        // null and the original NC array was left in place against a Block-List editor
        // (the persisted shape that breaks the indexer). We now throw.
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");
        // _contentTypeService.Get("missingType") returns null by default — no setup needed.

        var ncJson = @"[{""ncContentTypeAlias"":""missingType"",""key"":""abc"",""title"":""Hello""}]";

        var ex = await Assert.ThrowsExactlyAsync<PropertyConversionException>(
            () => InvokeConvertPropertyValue(ncJson, property.Object));
        Assert.AreEqual("blocks", ex.PropertyAlias);
        Assert.IsNull(ex.InnerKey);
        Assert.IsTrue(ex.Reason.Contains("no items could be converted", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithNestedMissingElementType_Throws()
    {
        // The inner-fallback bug: outer item resolves, but its "steps" property contains
        // a nested NC array referencing a missing element type. The old code silently
        // copied the raw NC array string into the resulting BL JSON — exactly the shape
        // that produces "Failed to add property 'steps' to index" in Examine. We now
        // throw with the inner key so the user knows WHICH property inside WHICH content
        // failed.
        var outerKey = Guid.NewGuid();
        SetupContentType("validOuter", outerKey);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        // Outer NC array → one item of validOuter, whose "steps" property is itself an
        // NC array referencing missingInner (which is not registered).
        var nestedNc = @"[{\""ncContentTypeAlias\"":\""missingInner\"",\""key\"":\""xyz\""}]";
        var outerJson = @"[{""ncContentTypeAlias"":""validOuter"",""key"":""abc"",""steps"":""" + nestedNc + @"""}]";

        var ex = await Assert.ThrowsExactlyAsync<PropertyConversionException>(
            () => InvokeConvertPropertyValue(outerJson, property.Object));
        Assert.AreEqual("blocks", ex.PropertyAlias);
        Assert.AreEqual("steps", ex.InnerKey);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithBlockListJObject_ReturnsNull()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("nestedContent");

        // A value already in Block List format (JObject, not JArray)
        var blockListJson = @"{""layout"":{""Umbraco.BlockList"":[{""contentUdi"":""umb://element/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee""}]},""contentData"":[{""contentTypeKey"":""11111111-2222-3333-4444-555555555555"",""udi"":""umb://element/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""title"":""Hello""}],""settingsData"":[]}";

        var result = await InvokeConvertPropertyValue(blockListJson, property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithDoubleEncodedInnerString_ReturnsRepairedJson()
    {
        // Regression test for the indexer error
        //   "Cannot assign value \"\"2022-09-09T14:00:00\"\" of type \"System.String\" to property
        //    \"dateTimeEndValue\" expecting type \"System.DateTime\""
        // The pre-87bb104 version of this converter ran Newtonsoft with default date parsing,
        // which caused inner property values that looked like ISO dates to be re-serialised with
        // an extra layer of quoting. The legacy database still contains values like
        //   "dateTimeEndValue": "\"2022-09-09T14:00:00\""
        // (literal quote characters as part of the C# string). Today's converter must detect that
        // shape on Thorough re-runs and emit a repaired Block List value instead of silently
        // returning null (which would leave the indexer broken forever).
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("datesAndTimesBL");

        // The actual shape from the user's uSync content config:
        // "dateTimeEndValue": "\"2022-09-09T14:00:00\"" — a JSON-encoded string whose
        // unescaped value is itself a quoted string.
        var brokenBl =
            @"{""layout"":{""Umbraco.BlockList"":[{""contentUdi"":""umb://element/2e9ce5f8b4b945acbf7e2767b22af455""}]}," +
            @"""contentData"":[{""contentTypeKey"":""b41215cd-5f6a-4d3e-b0d9-63edb202ed20""," +
            @"""dateTimeEndValue"":""\""2022-09-09T14:00:00\""""," +
            @"""dateTimeValue"":""\""2022-09-09T12:00:00\""""," +
            @"""udi"":""umb://element/2e9ce5f8b4b945acbf7e2767b22af455""}]," +
            @"""settingsData"":[]}";

        var result = await InvokeConvertPropertyValue(brokenBl, property.Object);

        Assert.IsNotNull(result, "Should return a repaired value, not null.");
        // Inspect the raw JSON string: Newtonsoft's default DateParseHandling would auto-convert
        // unquoted ISO date strings to DateTime when re-parsing into a JObject, which would
        // hide what's actually persisted. The thing we care about is exactly how the value
        // serialises, and the bug-vs-fix difference is precisely about an extra layer of quotes.
        var raw = result.ToString()!;
        Assert.IsTrue(
            raw.Contains(@"""dateTimeEndValue"":""2022-09-09T14:00:00"""),
            $"Expected single-quoted ISO date in output, got: {raw}");
        Assert.IsTrue(
            raw.Contains(@"""dateTimeValue"":""2022-09-09T12:00:00"""),
            $"Expected single-quoted ISO date in output, got: {raw}");
        // And the double-quoted form must be gone.
        Assert.IsFalse(
            raw.Contains(@"\""2022-09-09"),
            $"Double-encoded date should have been unwrapped, but output still contains escaped quotes: {raw}");
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithCleanBlockList_ReturnsNull()
    {
        // Counterpart to the previous test: a Block List value whose inner property values are
        // already clean must NOT be flagged as needing repair, otherwise Phase 4b would re-save
        // every already-converted item on every run.
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("datesAndTimes");

        var cleanBl =
            @"{""layout"":{""Umbraco.BlockList"":[{""contentUdi"":""umb://element/0bf3b4eb57bc4cc38e734b60f9cc1689""}]}," +
            @"""contentData"":[{""contentTypeKey"":""b41215cd-5f6a-4d3e-b0d9-63edb202ed20""," +
            @"""dateTimeEndValue"":""2022-09-09T14:00:00""," +
            @"""dateTimeValue"":""2022-09-09T12:00:00""," +
            @"""udi"":""umb://element/0bf3b4eb57bc4cc38e734b60f9cc1689""}]," +
            @"""settingsData"":[]}";

        var result = await InvokeConvertPropertyValue(cleanBl, property.Object);

        Assert.IsNull(result, "Clean Block List should be left alone (no save triggered).");
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithBlockListJObject_DoesNotLogError()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("nestedContent");

        var blockListJson = @"{""layout"":{""Umbraco.BlockList"":[{""contentUdi"":""umb://element/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee""}]},""contentData"":[{""contentTypeKey"":""11111111-2222-3333-4444-555555555555"",""udi"":""umb://element/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"",""title"":""Hello""}],""settingsData"":[]}";

        await InvokeConvertPropertyValue(blockListJson, property.Object);

        // Should not log any errors — this is a normal "already converted" case
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
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
    public async Task ConvertPropertyValue_DateValues_StoredAsUnquotedString()
    {
        // Regression: Newtonsoft's default DateParseHandling.DateTime auto-converts ISO date
        // strings into DateTime JTokens. The previous code then re-serialized them with
        // ToString(Formatting.None), producing a JSON-quoted string. When that string was
        // serialized again into the BlockList JSON, the surrounding quotes were escaped,
        // yielding "\"2025-01-15T00:00:00\"" — a double-quoted date in the raw JSON output.
        SetupContentType("dateBlock", Guid.NewGuid());

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("blocks");

        var ncJson = @"[{""ncContentTypeAlias"":""dateBlock"",""publishDate"":""2025-01-15T00:00:00""}]";

        var result = await InvokeConvertPropertyValue(ncJson, property.Object);

        Assert.IsNotNull(result);
        var rawBlockListJson = result.ToString()!;

        // The raw output JSON must contain the date as a normal string property:
        //   "publishDate":"2025-01-15T00:00:00"
        // and must NOT contain the double-quoted form:
        //   "publishDate":"\"2025-01-15T00:00:00\""
        StringAssert.Contains(rawBlockListJson, @"""publishDate"":""2025-01-15T00:00:00""",
            $"Date should be a plain string in the BlockList JSON. Got: {rawBlockListJson}");
        Assert.IsFalse(rawBlockListJson.Contains(@"""publishDate"":""\"""),
            $"Date must not be double-quoted in the BlockList JSON. Got: {rawBlockListJson}");
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
