using LP.Umbraco.LegacyFeatureConverter.Converters.MediaPicker;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Converters.MediaPicker;

[TestClass]
public class MediaPickerConverterTests
{
    private Mock<ILogger<MediaPickerConverter>> _loggerMock = null!;
    private Mock<IDataTypeService> _dataTypeServiceMock = null!;
    private Mock<IContentTypeService> _contentTypeServiceMock = null!;
    private Mock<IContentService> _contentServiceMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IScopeProvider> _scopeProviderMock = null!;
    private Mock<IMediaService> _mediaServiceMock = null!;
    private Mock<IDataValueEditorFactory> _dataValueEditorFactoryMock = null!;
    private Mock<PropertyEditorCollection> _propertyEditorCollectionMock = null!;
    private Mock<IConfigurationEditorJsonSerializer> _configSerializerMock = null!;

    private TestableMediaPickerConverter _converter = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MediaPickerConverter>>();
        _dataTypeServiceMock = new Mock<IDataTypeService>();
        _contentTypeServiceMock = new Mock<IContentTypeService>();
        _contentServiceMock = new Mock<IContentService>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _scopeProviderMock = new Mock<IScopeProvider>();
        _mediaServiceMock = new Mock<IMediaService>();
        _dataValueEditorFactoryMock = new Mock<IDataValueEditorFactory>();
        _propertyEditorCollectionMock = new Mock<PropertyEditorCollection>(() =>
            new PropertyEditorCollection(new DataEditorCollection(() => Enumerable.Empty<IDataEditor>())));
        _configSerializerMock = new Mock<IConfigurationEditorJsonSerializer>();

        _converter = new TestableMediaPickerConverter(
            _loggerMock.Object,
            _dataTypeServiceMock.Object,
            _contentTypeServiceMock.Object,
            _contentServiceMock.Object,
            _historyServiceMock.Object,
            _scopeProviderMock.Object,
            _mediaServiceMock.Object,
            _dataValueEditorFactoryMock.Object,
            _propertyEditorCollectionMock.Object,
            _configSerializerMock.Object);
    }

    /// <summary>
    /// Sets up a media item in the mock service with the given key and type alias.
    /// </summary>
    private void SetupMedia(Guid key, string typeAlias = "Image")
    {
        var mediaType = new Mock<ISimpleContentType>();
        mediaType.Setup(t => t.Alias).Returns(typeAlias);

        var media = new Mock<IMedia>();
        media.Setup(m => m.ContentType).Returns(mediaType.Object);

        _mediaServiceMock.Setup(s => s.GetById(key)).Returns(media.Object);
    }

    [TestMethod]
    public void Properties_ReturnExpectedValues()
    {
        Assert.AreEqual("Legacy Media Picker to MediaPicker3", _converter.ConverterName);
        Assert.AreEqual("Umbraco.MediaPicker3", _converter.TargetPropertyEditorAlias);
        CollectionAssert.Contains(_converter.SourcePropertyEditorAliases, "Umbraco.MediaPicker2");
    }

    // ===== ConvertPropertyValueAsync tests =====

    [TestMethod]
    public async Task ConvertPropertyValue_WithNull_ReturnsEmptyArray()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var result = await _converter.TestConvertPropertyValueAsync(null!, property.Object);

        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_WithEmptyString_ReturnsEmptyArray()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var result = await _converter.TestConvertPropertyValueAsync("", property.Object);

        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_AlreadyMediaPicker3_ReturnsNull()
    {
        // Already in MP3 format → ConvertPropertyValueAsync returns null,
        // which signals BasePropertyConverter to leave the value unchanged.
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var mp3Json = @"[{""key"":""abc"",""mediaKey"":""def""}]";

        var result = await _converter.TestConvertPropertyValueAsync(mp3Json, property.Object);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_SingleUdi_ProducesValidMediaPicker3()
    {
        var mediaKey = Guid.NewGuid();
        SetupMedia(mediaKey, "Image");

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var udiString = $"umb://media/{mediaKey:N}";

        var result = await _converter.TestConvertPropertyValueAsync(udiString, property.Object);

        Assert.IsNotNull(result);
        var items = JArray.Parse(result.ToString()!);

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(mediaKey.ToString(), items[0]["mediaKey"]!.ToString());
        Assert.AreEqual("Image", items[0]["mediaTypeAlias"]!.ToString());
        Assert.IsNotNull(items[0]["key"]);
        Assert.IsNotNull(items[0]["focalPoint"]);
        Assert.AreEqual(0.5m, items[0]["focalPoint"]!["left"]!.Value<decimal>());
        Assert.AreEqual(0.5m, items[0]["focalPoint"]!["top"]!.Value<decimal>());
    }

    [TestMethod]
    public async Task ConvertPropertyValue_MultipleUdis_ProducesMultipleItems()
    {
        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();
        SetupMedia(key1, "Image");
        SetupMedia(key2, "File");

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("images");

        var udiString = $"umb://media/{key1:N},umb://media/{key2:N}";

        var result = await _converter.TestConvertPropertyValueAsync(udiString, property.Object);

        var items = JArray.Parse(result!.ToString()!);

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual(key1.ToString(), items[0]["mediaKey"]!.ToString());
        Assert.AreEqual("Image", items[0]["mediaTypeAlias"]!.ToString());
        Assert.AreEqual(key2.ToString(), items[1]["mediaKey"]!.ToString());
        Assert.AreEqual("File", items[1]["mediaTypeAlias"]!.ToString());
    }

    [TestMethod]
    public async Task ConvertPropertyValue_InvalidUdi_SkipsItem()
    {
        var validKey = Guid.NewGuid();
        SetupMedia(validKey);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var udiString = $"not-a-udi,umb://media/{validKey:N}";

        var result = await _converter.TestConvertPropertyValueAsync(udiString, property.Object);

        var items = JArray.Parse(result!.ToString()!);

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(validKey.ToString(), items[0]["mediaKey"]!.ToString());
    }

    [TestMethod]
    public async Task ConvertPropertyValue_AllInvalidUdis_ReturnsEmptyArray()
    {
        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var result = await _converter.TestConvertPropertyValueAsync("not-a-udi,also-invalid", property.Object);

        Assert.AreEqual("[]", result);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_MediaNotFound_StillIncludesItem()
    {
        var mediaKey = Guid.NewGuid();
        // Don't set up media — GetById will return null

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var udiString = $"umb://media/{mediaKey:N}";

        var result = await _converter.TestConvertPropertyValueAsync(udiString, property.Object);

        var items = JArray.Parse(result!.ToString()!);

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual(mediaKey.ToString(), items[0]["mediaKey"]!.ToString());
        Assert.IsTrue(items[0]["mediaTypeAlias"]!.Type == JTokenType.Null);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_UdisWithWhitespace_TrimmedCorrectly()
    {
        var key = Guid.NewGuid();
        SetupMedia(key);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var udiString = $"  umb://media/{key:N}  ";

        var result = await _converter.TestConvertPropertyValueAsync(udiString, property.Object);

        var items = JArray.Parse(result!.ToString()!);
        Assert.AreEqual(1, items.Count);
    }

    [TestMethod]
    public async Task ConvertPropertyValue_EmptyCropsArray()
    {
        var key = Guid.NewGuid();
        SetupMedia(key);

        var property = new Mock<IProperty>();
        property.Setup(p => p.Alias).Returns("image");

        var result = await _converter.TestConvertPropertyValueAsync(
            $"umb://media/{key:N}", property.Object);

        var items = JArray.Parse(result!.ToString()!);
        var crops = items[0]["crops"] as JArray;

        Assert.IsNotNull(crops);
        Assert.AreEqual(0, crops.Count);
    }
}

/// <summary>
/// Testable subclass that exposes the protected ConvertPropertyValueAsync method.
/// </summary>
internal class TestableMediaPickerConverter : MediaPickerConverter
{
    public TestableMediaPickerConverter(
        ILogger<MediaPickerConverter> logger,
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IConversionHistoryService historyService,
        IScopeProvider scopeProvider,
        IMediaService mediaService,
        IDataValueEditorFactory dataValueEditorFactory,
        PropertyEditorCollection propertyEditorCollection,
        IConfigurationEditorJsonSerializer configurationEditorJsonSerializer)
        : base(logger, dataTypeService, contentTypeService, contentService, historyService,
            scopeProvider, mediaService, dataValueEditorFactory, propertyEditorCollection,
            configurationEditorJsonSerializer)
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
