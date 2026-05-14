using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Infrastructure.Services;

[TestClass]
public class ConverterServiceTests
{
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<ILogger<ConverterService>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _loggerMock = new Mock<ILogger<ConverterService>>();
    }

    /// <summary>
    /// Creates a fake converter for testing.
    /// </summary>
    private static Mock<IPropertyConverter> CreateFakeConverter(
        string name, string[] sourceAliases, string targetAlias, string description = "Test",
        string? shortName = null, string icon = "icon-axis-rotation", string category = "Property editor")
    {
        var mock = new Mock<IPropertyConverter>();
        mock.Setup(c => c.ConverterName).Returns(name);
        mock.Setup(c => c.SourcePropertyEditorAliases).Returns(sourceAliases);
        mock.Setup(c => c.TargetPropertyEditorAlias).Returns(targetAlias);
        mock.Setup(c => c.Description).Returns(description);
        mock.Setup(c => c.ShortName).Returns(shortName ?? name);
        mock.Setup(c => c.Icon).Returns(icon);
        mock.Setup(c => c.Category).Returns(category);
        mock.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return mock;
    }

    /// <summary>
    /// Creates a ConverterService with the given property converters and no macro converters.
    /// </summary>
    private ConverterService CreateService(params IPropertyConverter[] converters)
    {
        return new ConverterService(
            converters,
            Array.Empty<IMacroConverter>(),
            _contentTypeServiceMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Creates a ConverterService with both property converters and macro converters.
    /// </summary>
    private ConverterService CreateService(
        IPropertyConverter[] propertyConverters,
        IMacroConverter[] macroConverters)
    {
        return new ConverterService(
            propertyConverters,
            macroConverters,
            _contentTypeServiceMock.Object,
            _loggerMock.Object);
    }

    /// <summary>
    /// Creates a fake macro converter for testing.
    /// </summary>
    private static Mock<IMacroConverter> CreateFakeMacroConverter(
        string name,
        string targetShape = "richTextBlock",
        string description = "Test macro converter",
        int affectedCount = 0)
    {
        var mock = new Mock<IMacroConverter>();
        mock.Setup(c => c.ConverterName).Returns(name);
        mock.Setup(c => c.Description).Returns(description);
        mock.Setup(c => c.ShortName).Returns(name);
        mock.Setup(c => c.Icon).Returns("icon-code");
        mock.Setup(c => c.Category).Returns("Macro");
        mock.Setup(c => c.TargetShapeAlias).Returns(targetShape);
        mock.Setup(c => c.GetAffectedUnitCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(affectedCount);
        return mock;
    }

    [TestMethod]
    public void GetAllConverters_ReturnsAllRegistered()
    {
        var c1 = CreateFakeConverter("Converter A", ["Alias.A"], "Alias.B");
        var c2 = CreateFakeConverter("Converter B", ["Alias.C"], "Alias.D");
        var service = CreateService(c1.Object, c2.Object);

        var result = service.GetAllConverters().ToList();

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetAllConverters_WithNoConverters_ReturnsEmpty()
    {
        var service = CreateService();

        var result = service.GetAllConverters().ToList();

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetConverterByName_FindsExisting_CaseInsensitive()
    {
        var converter = CreateFakeConverter("Nested Content to Block List", ["NC"], "BL");
        var service = CreateService(converter.Object);

        var found = service.GetConverterByName("nested content to block list");

        Assert.IsNotNull(found);
        Assert.AreEqual("Nested Content to Block List", found.ConverterName);
    }

    [TestMethod]
    public void GetConverterByName_ReturnsNull_WhenNotFound()
    {
        var converter = CreateFakeConverter("Test", ["A"], "B");
        var service = CreateService(converter.Object);

        var found = service.GetConverterByName("Nonexistent");

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task GetConverterMetadataAsync_ReturnsMetadataForAll()
    {
        var c1 = CreateFakeConverter("Conv A", ["Alias.A"], "Alias.B", "Description A");
        c1.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var c2 = CreateFakeConverter("Conv B", ["Alias.C"], "Alias.D", "Description B");
        c2.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var service = CreateService(c1.Object, c2.Object);

        var metadata = (await service.GetConverterMetadataAsync()).ToList();

        Assert.AreEqual(2, metadata.Count);
        Assert.AreEqual("Conv A", metadata[0].Name);
        Assert.AreEqual(3, metadata[0].AffectedDocumentTypesCount);
        Assert.AreEqual("Conv B", metadata[1].Name);
        Assert.AreEqual(5, metadata[1].AffectedDocumentTypesCount);
    }

    [TestMethod]
    public async Task GetConverterMetadataAsync_IncludesShortNameIconAndCategory()
    {
        var converter = CreateFakeConverter(
            "Nested Content to Block List", ["NC"], "BL", "Converts NC to BL",
            shortName: "Nested Content", icon: "icon-list", category: "Property editor");
        converter.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService(converter.Object);

        var metadata = (await service.GetConverterMetadataAsync()).ToList();

        Assert.AreEqual(1, metadata.Count);
        Assert.AreEqual("Nested Content", metadata[0].ShortName);
        Assert.AreEqual("icon-list", metadata[0].Icon);
        Assert.AreEqual("Property editor", metadata[0].Category);
    }

    [TestMethod]
    public async Task GetConverterMetadataAsync_SkipsFailedConverters()
    {
        var c1 = CreateFakeConverter("Working", ["A"], "B");
        c1.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var c2 = CreateFakeConverter("Broken", ["C"], "D");
        c2.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Boom"));

        var service = CreateService(c1.Object, c2.Object);

        var metadata = (await service.GetConverterMetadataAsync()).ToList();

        Assert.AreEqual(1, metadata.Count);
        Assert.AreEqual("Working", metadata[0].Name);
    }

    [TestMethod]
    public async Task GetAffectedDocumentTypesAsync_ReturnsEmpty_WhenConverterNotFound()
    {
        var service = CreateService();

        var result = (await service.GetAffectedDocumentTypesAsync("Nonexistent")).ToList();

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetAffectedDocumentTypesAsync_ReturnsDocTypesWithMatchingProperties()
    {
        var converter = CreateFakeConverter("Test", ["Umbraco.NestedContent"], "Umbraco.BlockList");

        // Create a mock doc type with a matching property
        var propertyType = new Mock<IPropertyType>();
        propertyType.Setup(p => p.PropertyEditorAlias).Returns("Umbraco.NestedContent");

        var docType = new Mock<IContentType>();
        docType.Setup(d => d.Key).Returns(Guid.NewGuid());
        docType.Setup(d => d.Name).Returns("HomePage");
        docType.Setup(d => d.Alias).Returns("homePage");
        docType.Setup(d => d.Icon).Returns("icon-home");
        docType.Setup(d => d.PropertyTypes).Returns(new[] { propertyType.Object });
        docType.Setup(d => d.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());

        _contentTypeServiceMock.Setup(s => s.GetAll())
            .Returns(new[] { docType.Object });

        var service = CreateService(converter.Object);

        var result = (await service.GetAffectedDocumentTypesAsync("Test")).ToList();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("HomePage", result[0].Name);
        Assert.AreEqual(1, result[0].PropertiesCount);
    }

    [TestMethod]
    public void GetAllLegacyConverters_ReturnsPropertyConvertersAsLegacyBase()
    {
        var c1 = CreateFakeConverter("Conv A", ["Alias.A"], "Alias.B");
        var c2 = CreateFakeConverter("Conv B", ["Alias.C"], "Alias.D");
        var service = CreateService(c1.Object, c2.Object);

        var result = service.GetAllLegacyConverters().ToList();

        Assert.AreEqual(2, result.Count);
        Assert.IsInstanceOfType<ILegacyFeatureConverter>(result[0]);
        Assert.IsInstanceOfType<ILegacyFeatureConverter>(result[1]);
    }

    [TestMethod]
    public void GetAllLegacyConverters_MergesPropertyAndMacroFamilies()
    {
        var prop = CreateFakeConverter("Property A", ["A"], "B");
        var macro = CreateFakeMacroConverter("Macro A");
        var service = CreateService(new[] { prop.Object }, new[] { macro.Object });

        var result = service.GetAllLegacyConverters().ToList();

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(r => r.ConverterName == "Property A"));
        Assert.IsTrue(result.Any(r => r.ConverterName == "Macro A"));
    }

    [TestMethod]
    public void GetLegacyConverterByName_FindsMacroConverter_CaseInsensitive()
    {
        var prop = CreateFakeConverter("Property A", ["A"], "B");
        var macro = CreateFakeMacroConverter("Macro to Rich Text Block");
        var service = CreateService(new[] { prop.Object }, new[] { macro.Object });

        var found = service.GetLegacyConverterByName("macro to rich text block");

        Assert.IsNotNull(found);
        Assert.AreEqual("Macro to Rich Text Block", found.ConverterName);
        Assert.IsInstanceOfType<IMacroConverter>(found);
    }

    [TestMethod]
    public async Task GetConverterMetadataAsync_IncludesMacroConverters()
    {
        var prop = CreateFakeConverter("Property A", ["A"], "B");
        prop.Setup(c => c.GetAffectedDocumentTypesCountAsync(
                It.IsAny<Guid[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        var macro = CreateFakeMacroConverter("Macro to RTB", affectedCount: 7);

        var service = CreateService(new[] { prop.Object }, new[] { macro.Object });

        var metadata = (await service.GetConverterMetadataAsync()).ToList();

        Assert.AreEqual(2, metadata.Count);
        var propMeta = metadata.Single(m => m.Name == "Property A");
        Assert.AreEqual("Property editor", propMeta.Category);
        Assert.AreEqual(4, propMeta.AffectedDocumentTypesCount);
        var macroMeta = metadata.Single(m => m.Name == "Macro to RTB");
        Assert.AreEqual("Macro", macroMeta.Category);
        Assert.AreEqual(7, macroMeta.AffectedDocumentTypesCount);
        Assert.AreEqual("richTextBlock", macroMeta.TargetAlias);
        Assert.AreEqual(0, macroMeta.SourceAliases.Length);
    }

    [TestMethod]
    public void GetLegacyConverterByName_FindsConverter_CaseInsensitive()
    {
        var converter = CreateFakeConverter("Nested Content to Block List", ["NC"], "BL");
        var service = CreateService(converter.Object);

        var found = service.GetLegacyConverterByName("NESTED content to block LIST");

        Assert.IsNotNull(found);
        Assert.AreEqual("Nested Content to Block List", found.ConverterName);
    }

    [TestMethod]
    public void GetLegacyConverterByName_ReturnsNull_WhenNotFound()
    {
        var converter = CreateFakeConverter("Test", ["A"], "B");
        var service = CreateService(converter.Object);

        var found = service.GetLegacyConverterByName("Nonexistent");

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task GetAffectedDocumentTypesAsync_ResultsOrderedByName()
    {
        var converter = CreateFakeConverter("Test", ["Umbraco.NC"], "Umbraco.BL");

        var createDocType = (string name) =>
        {
            var pt = new Mock<IPropertyType>();
            pt.Setup(p => p.PropertyEditorAlias).Returns("Umbraco.NC");
            var dt = new Mock<IContentType>();
            dt.Setup(d => d.Key).Returns(Guid.NewGuid());
            dt.Setup(d => d.Name).Returns(name);
            dt.Setup(d => d.Alias).Returns(name.ToLower());
            dt.Setup(d => d.PropertyTypes).Returns(new[] { pt.Object });
            dt.Setup(d => d.CompositionPropertyTypes).Returns(Enumerable.Empty<IPropertyType>());
            return dt.Object;
        };

        _contentTypeServiceMock.Setup(s => s.GetAll())
            .Returns(new[] { createDocType("Zebra"), createDocType("Apple"), createDocType("Mango") });

        var service = CreateService(converter.Object);
        var result = (await service.GetAffectedDocumentTypesAsync("Test")).ToList();

        Assert.AreEqual("Apple", result[0].Name);
        Assert.AreEqual("Mango", result[1].Name);
        Assert.AreEqual("Zebra", result[2].Name);
    }
}
