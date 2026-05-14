using Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Infrastructure.Services;

/// <summary>
/// Tests for <see cref="MacroConverterQueryService"/>. Most callers (the macro converter,
/// the controller) interact through the mocked interface; this test class exercises the
/// real implementation against mocked Umbraco services to catch issues in the scan logic
/// itself.
/// </summary>
[TestClass]
public class MacroConverterQueryServiceTests
{
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IMacroService> _macroServiceMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IJsonSerializer> _jsonSerializerMock = null!;
    private Mock<ILogger<MacroConverterQueryService>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _macroServiceMock = new Mock<IMacroService>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _jsonSerializerMock = new Mock<IJsonSerializer>();
        _loggerMock = new Mock<ILogger<MacroConverterQueryService>>();
    }

    private MacroConverterQueryService CreateService()
        => new(_contentTypeServiceMock.Object, _contentServiceMock.Object,
              _macroServiceMock.Object, _dataTypeServiceMock.Object,
              _jsonSerializerMock.Object, _loggerMock.Object);

    /// <summary>
    /// Regression: <c>IContentTypeComposition.CompositionPropertyTypes</c> in Umbraco v13 can
    /// return the same property alias more than once. The <c>.Union(PropertyTypes)</c> in
    /// Umbraco's implementation only dedupes by reference; the cloned inherited properties
    /// have different references, so two compositions that share a common ancestor
    /// (diamond inheritance) produce duplicate-aliased entries.
    ///
    /// The scan and content-rewrite code paths use <c>.ToDictionary(pt =&gt; pt.Alias, ...)</c>
    /// to look up properties on block element types, which throws
    /// "An item with the same key has already been added" the moment it sees the first
    /// duplicate alias. The fix is a <c>.DistinctBy(pt =&gt; pt.Alias, ...)</c> guard before
    /// every such ToDictionary call.
    ///
    /// This test exercises the block-content scan path with an element type whose
    /// <c>CompositionPropertyTypes</c> reports the same alias twice (two different mock
    /// references — exactly as Umbraco would in a diamond-inheritance scenario).
    /// </summary>
    [TestMethod]
    public async Task ScanForMacroUsageAsync_BlockElementTypeWithDuplicateCompositionAlias_DoesNotThrow()
    {
        // Macro registry has one macro so the scan proceeds past its early-out.
        var macro = new Mock<IMacro>();
        macro.Setup(m => m.Alias).Returns("contactForm");
        macro.Setup(m => m.Name).Returns("Contact Form");
        macro.Setup(m => m.Key).Returns(Guid.NewGuid());
        _macroServiceMock.Setup(s => s.GetAll())
            .Returns(new[] { macro.Object });

        // Macro-enabled RTE data type that the BLOCK ELEMENT TYPE will use for its RTE property.
        var rteDataTypeId = 42;
        var rteDataType = new Mock<IDataType>();
        rteDataType.Setup(d => d.Id).Returns(rteDataTypeId);
        rteDataType.Setup(d => d.Key).Returns(Guid.NewGuid());
        rteDataType.Setup(d => d.Name).Returns("RichText");
        rteDataType.Setup(d => d.EditorAlias).Returns(Constants.PropertyEditors.Aliases.TinyMce);
        rteDataType.Setup(d => d.Configuration).Returns(new RichTextConfiguration
        {
            Editor = JObject.Parse("{ \"toolbar\": [\"umbmacro\"] }")
        });
        _dataTypeServiceMock.Setup(s => s.GetByEditorAlias(Constants.PropertyEditors.Aliases.TinyMce))
            .Returns(new[] { rteDataType.Object });

        // The top-level doc type has a BlockList property. The scan walks its content,
        // then walks the BlockList JSON, then looks at the block element type's
        // CompositionPropertyTypes — that's where the duplicate-alias issue bites.
        var blockListProperty = new Mock<IPropertyType>();
        blockListProperty.Setup(pt => pt.Alias).Returns("blocks");
        blockListProperty.Setup(pt => pt.PropertyEditorAlias).Returns(Constants.PropertyEditors.Aliases.BlockList);
        blockListProperty.Setup(pt => pt.DataTypeId).Returns(999);

        var docType = new Mock<IContentType>();
        docType.Setup(c => c.Id).Returns(1);
        docType.Setup(c => c.Alias).Returns("article");
        docType.Setup(c => c.PropertyTypes).Returns(new[] { blockListProperty.Object });
        docType.Setup(c => c.CompositionPropertyTypes).Returns(new[] { blockListProperty.Object });

        // Block ELEMENT type: this is the one with the diamond-inheritance duplicate alias.
        // Two property instances, same alias "dateTimeValue", different mock references —
        // exactly what Umbraco produces when two compositions share a common ancestor.
        var elementTypeKey = Guid.NewGuid();

        var dupA = new Mock<IPropertyType>();
        dupA.Setup(pt => pt.Alias).Returns("dateTimeValue");
        dupA.Setup(pt => pt.PropertyEditorAlias).Returns(Constants.PropertyEditors.Aliases.TinyMce);
        dupA.Setup(pt => pt.DataTypeId).Returns(rteDataTypeId);

        var dupB = new Mock<IPropertyType>();
        dupB.Setup(pt => pt.Alias).Returns("dateTimeValue"); // same alias!
        dupB.Setup(pt => pt.PropertyEditorAlias).Returns(Constants.PropertyEditors.Aliases.TinyMce);
        dupB.Setup(pt => pt.DataTypeId).Returns(rteDataTypeId);

        var elementType = new Mock<IContentType>();
        elementType.Setup(c => c.Id).Returns(2);
        elementType.Setup(c => c.Key).Returns(elementTypeKey);
        elementType.Setup(c => c.Alias).Returns("dateBlock");
        elementType.Setup(c => c.IsElement).Returns(true);
        elementType.Setup(c => c.PropertyTypes).Returns(new[] { dupA.Object });
        elementType.Setup(c => c.CompositionPropertyTypes).Returns(new[] { dupA.Object, dupB.Object });

        _contentTypeServiceMock.Setup(s => s.GetAll())
            .Returns(new[] { docType.Object, elementType.Object });
        _contentTypeServiceMock.Setup(s => s.Get(elementTypeKey))
            .Returns(elementType.Object);

        // One content node with a BlockList value that references the element type.
        var content = new Mock<IContent>();
        content.Setup(c => c.Id).Returns(100);
        content.Setup(c => c.Key).Returns(Guid.NewGuid());
        content.Setup(c => c.Name).Returns("Test Page");

        var blockListJson = $@"{{
            ""layout"": {{ ""Umbraco.BlockList"": [{{ ""contentUdi"": ""umb://element/{Guid.NewGuid():N}"" }}] }},
            ""contentData"": [{{
                ""contentTypeKey"": ""{elementTypeKey}"",
                ""udi"": ""umb://element/{Guid.NewGuid():N}"",
                ""dateTimeValue"": ""<p>some markup</p>""
            }}],
            ""settingsData"": []
        }}";

        var blockListPropertyValue = new Mock<IProperty>();
        var ptForProperty = new Mock<IPropertyType>();
        ptForProperty.Setup(pt => pt.Variations).Returns(ContentVariation.Nothing);
        blockListPropertyValue.Setup(p => p.Alias).Returns("blocks");
        blockListPropertyValue.Setup(p => p.PropertyType).Returns(ptForProperty.Object);
        blockListPropertyValue.Setup(p => p.GetValue(null, null)).Returns(blockListJson);

        var propertyCollectionMock = new Mock<IPropertyCollection>();
        propertyCollectionMock.As<IEnumerable<IProperty>>()
            .Setup(x => x.GetEnumerator())
            .Returns(() => new List<IProperty> { blockListPropertyValue.Object }.GetEnumerator());
        content.Setup(c => c.Properties).Returns(propertyCollectionMock.Object);

        long totalRecords = 1;
        _contentServiceMock
            .Setup(s => s.GetPagedOfType(1, 0, int.MaxValue,
                out totalRecords, It.IsAny<global::Umbraco.Cms.Core.Persistence.Querying.IQuery<IContent>>()))
            .Returns(new[] { content.Object });

        var service = CreateService();

        // Pre-fix this throws ArgumentException("An item with the same key has already been added. Key: dateTimeValue")
        // inside ScanBlockValueForMacros' ToDictionary on the duplicate-aliased CompositionPropertyTypes.
        var result = await service.ScanForMacroUsageAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.AliasUsage.Count);
        Assert.AreEqual("contactForm", result.AliasUsage[0].Alias);
    }
}
