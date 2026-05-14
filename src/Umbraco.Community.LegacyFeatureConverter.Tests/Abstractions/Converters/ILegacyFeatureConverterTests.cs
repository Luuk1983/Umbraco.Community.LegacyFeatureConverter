using Umbraco.Community.LegacyFeatureConverter.Converters;
using Microsoft.Extensions.Logging;
using Moq;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Converters;

/// <summary>
/// Tests for the shared <see cref="ILegacyFeatureConverter"/> base interface.
///
/// Verifies that the family-agnostic metadata contract is correctly inherited
/// by the specialized converter interfaces (e.g. <see cref="IPropertyConverter"/>)
/// and that the default <c>ShortName</c>/<c>Icon</c>/<c>Category</c> members
/// behave as documented.
/// </summary>
[TestClass]
public class ILegacyFeatureConverterTests
{
    [TestMethod]
    public void IPropertyConverter_Extends_ILegacyFeatureConverter()
    {
        Assert.IsTrue(
            typeof(ILegacyFeatureConverter).IsAssignableFrom(typeof(IPropertyConverter)),
            "IPropertyConverter should extend ILegacyFeatureConverter so they share metadata.");
    }

    [TestMethod]
    public void TestableConverter_ExposesMetadata_AsILegacyFeatureConverter()
    {
        var converter = CreatePropertyConverter();
        ILegacyFeatureConverter asBase = converter;

        Assert.AreEqual("Test Converter", asBase.ConverterName);
        Assert.AreEqual("A test converter for unit testing.", asBase.Description);
        Assert.AreEqual("Test Converter", asBase.ShortName); // defaults to ConverterName
        Assert.AreEqual("icon-axis-rotation", asBase.Icon);   // default
        Assert.AreEqual("Property editor", asBase.Category);  // default
    }

    [TestMethod]
    public async Task GetAffectedUnitCountAsync_OnPropertyConverter_ForwardsToDocTypeCount()
    {
        var (converter, _, contentTypeServiceMock) = CreateInstrumentedConverter();
        contentTypeServiceMock
            .Setup(x => x.GetAll())
            .Returns(System.Linq.Enumerable.Empty<Umbraco.Cms.Core.Models.IContentType>());

        // Cast to the base interface so we are testing through the shared contract.
        ILegacyFeatureConverter asBase = converter;
        var count = await asBase.GetAffectedUnitCountAsync();

        Assert.AreEqual(0, count);
        contentTypeServiceMock.Verify(x => x.GetAll(), Times.Once,
            "GetAffectedUnitCountAsync on a property converter must forward to the document-type scan.");
    }

    private static TestableConverter CreatePropertyConverter()
    {
        return CreateInstrumentedConverter().Converter;
    }

    private static (TestableConverter Converter, Mock<ILogger> Logger, Mock<IContentTypeService> ContentTypeService) CreateInstrumentedConverter()
    {
        var loggerMock = new Mock<ILogger>();
        var dataTypeServiceMock = new Mock<Umbraco.Cms.Core.Services.IDataTypeService>();
        var contentTypeServiceMock = new Mock<IContentTypeService>();
        var contentServiceMock = new Mock<IContentService>();
        var historyServiceMock = new Mock<Umbraco.Community.LegacyFeatureConverter.Services.IConversionHistoryService>();
        var scopeProviderMock = new Mock<IScopeProvider>();

        var converter = new TestableConverter(
            loggerMock.Object,
            dataTypeServiceMock.Object,
            contentTypeServiceMock.Object,
            contentServiceMock.Object,
            historyServiceMock.Object,
            scopeProviderMock.Object);

        return (converter, loggerMock, contentTypeServiceMock);
    }
}
