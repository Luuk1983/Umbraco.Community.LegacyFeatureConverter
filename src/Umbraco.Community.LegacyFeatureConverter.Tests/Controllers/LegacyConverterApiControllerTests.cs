using Umbraco.Community.LegacyFeatureConverter.Controllers;
using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Dtos;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Controllers;

[TestClass]
public class LegacyConverterApiControllerTests
{
    private Mock<IConverterService> _converterServiceMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IConversionQueueService> _queueServiceMock = null!;
    private Mock<IMacroConverterQueryService> _macroQueryServiceMock = null!;
    private Mock<ILogger<LegacyConverterApiController>> _loggerMock = null!;
    private LegacyConverterApiController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _converterServiceMock = new Mock<IConverterService>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _queueServiceMock = new Mock<IConversionQueueService>();
        _macroQueryServiceMock = new Mock<IMacroConverterQueryService>();
        _loggerMock = new Mock<ILogger<LegacyConverterApiController>>();

        _controller = new LegacyConverterApiController(
            _converterServiceMock.Object,
            _historyServiceMock.Object,
            _queueServiceMock.Object,
            _macroQueryServiceMock.Object,
            _loggerMock.Object);

        // Set up a default HttpContext so GetCurrentUserKey() doesn't throw
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    // ===== GetConverters =====

    [TestMethod]
    public async Task GetConverters_ReturnsOk_WithMetadata()
    {
        var metadata = new List<ConverterMetadata>
        {
            new() { Name = "NC to BL", Description = "Test" }
        };
        _converterServiceMock.Setup(s => s.GetConverterMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var result = await _controller.GetConverters();

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ===== GetDocumentTypes =====

    [TestMethod]
    public async Task GetDocumentTypes_WithEmptyName_ReturnsBadRequest()
    {
        var result = await _controller.GetDocumentTypes("");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDocumentTypes_WithUnknownConverter_ReturnsNotFound()
    {
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Unknown"))
            .Returns((IPropertyConverter?)null);

        var result = await _controller.GetDocumentTypes("Unknown");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDocumentTypes_WithValidConverter_ReturnsOk()
    {
        var converterMock = new Mock<IPropertyConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("NC to BL"))
            .Returns(converterMock.Object);
        _converterServiceMock.Setup(s => s.GetAffectedDocumentTypesAsync(
                "NC to BL", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentTypeInfo>());

        var result = await _controller.GetDocumentTypes("NC to BL");

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // ===== QueueConversion =====

    [TestMethod]
    public async Task QueueConversion_WithNullBody_ReturnsBadRequest()
    {
        var result = await _controller.QueueConversion(null);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task QueueConversion_WithEmptyConverterType_ReturnsBadRequest()
    {
        var request = new ConversionRequestDto { ConverterType = "" };

        var result = await _controller.QueueConversion(request);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task QueueConversion_WithUnknownConverter_ReturnsNotFound()
    {
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Unknown"))
            .Returns((IPropertyConverter?)null);

        var request = new ConversionRequestDto { ConverterType = "Unknown" };

        var result = await _controller.QueueConversion(request);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task QueueConversion_WithValidRequest_ReturnsOk_WithQueueItemId()
    {
        var queueItemId = Guid.NewGuid();
        var converterMock = new Mock<IPropertyConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("NC to BL"))
            .Returns(converterMock.Object);
        _queueServiceMock.Setup(s => s.EnqueueAsync(
                It.IsAny<ConversionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItemId);

        var request = new ConversionRequestDto
        {
            ConverterType = "NC to BL",
            IsTestRun = true,
            StopOnError = false,
            RunTestFirst = true
        };

        var result = await _controller.QueueConversion(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);

        // Verify the queue service was called with correct options
        _queueServiceMock.Verify(s => s.EnqueueAsync(
            It.Is<ConversionOptions>(o =>
                o.ConverterType == "NC to BL" &&
                o.IsTestRun == true &&
                o.StopOnError == false &&
                o.RunTestFirst == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task QueueConversion_WithPublishAfterConversion_MapsOptionCorrectly()
    {
        var queueItemId = Guid.NewGuid();
        var converterMock = new Mock<IPropertyConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("NC to BL"))
            .Returns(converterMock.Object);
        _queueServiceMock.Setup(s => s.EnqueueAsync(
                It.IsAny<ConversionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItemId);

        var request = new ConversionRequestDto
        {
            ConverterType = "NC to BL",
            PublishAfterConversion = true
        };

        var result = await _controller.QueueConversion(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _queueServiceMock.Verify(s => s.EnqueueAsync(
            It.Is<ConversionOptions>(o => o.PublishAfterConversion == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task QueueConversion_WithMacroOptions_PersistsAllMacroFields()
    {
        var queueItemId = Guid.NewGuid();
        var converterMock = new Mock<IMacroConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Macro to Rich Text Block"))
            .Returns(converterMock.Object);
        _queueServiceMock.Setup(s => s.EnqueueAsync(
                It.IsAny<ConversionOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItemId);

        var macroKey = Guid.NewGuid();
        var request = new ConversionRequestDto
        {
            ConverterType = "Macro to Rich Text Block",
            SelectedMacroKeys = new[] { macroKey },
            GenerateStubPartialViews = false
        };

        var result = await _controller.QueueConversion(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        _queueServiceMock.Verify(s => s.EnqueueAsync(
            It.Is<ConversionOptions>(o =>
                o.SelectedMacroKeys != null
                && o.SelectedMacroKeys.Length == 1
                && o.SelectedMacroKeys[0] == macroKey
                && o.GenerateStubPartialViews == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===== GetMacros =====

    [TestMethod]
    public async Task GetMacros_WithMacroConverter_ReturnsMacroList()
    {
        var converterMock = new Mock<IMacroConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Macro to Rich Text Block"))
            .Returns(converterMock.Object);

        var knownMacro = new Mock<Umbraco.Cms.Core.Models.IMacro>();
        knownMacro.Setup(m => m.Alias).Returns("contactForm");
        knownMacro.Setup(m => m.Name).Returns("Contact Form");
        knownMacro.Setup(m => m.Key).Returns(Guid.NewGuid());

        var secondMacro = new Mock<Umbraco.Cms.Core.Models.IMacro>();
        secondMacro.Setup(m => m.Alias).Returns("zeroUsage");
        secondMacro.Setup(m => m.Name).Returns("Zero Usage");
        secondMacro.Setup(m => m.Key).Returns(Guid.NewGuid());

        _macroQueryServiceMock.Setup(s => s.ScanForMacroUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MacroScanResult
            {
                AliasUsage = new List<MacroAliasUsage>
                {
                    new() { Alias = "contactForm", Macro = knownMacro.Object, UsageCount = 4, RtePropertyCount = 3 },
                    new() { Alias = "zeroUsage", Macro = secondMacro.Object, UsageCount = 0, RtePropertyCount = 0 }
                }
            });

        var result = await _controller.GetMacros("Macro to Rich Text Block");
        var json = (JsonResult)result;
        var list = (List<MacroInfoDto>)json.Value!;

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("Contact Form", list[0].Name);
        Assert.AreEqual(4, list[0].UsageCount);
        Assert.AreEqual("Zero Usage", list[1].Name);
        Assert.AreEqual(0, list[1].UsageCount);
    }

    [TestMethod]
    public async Task GetMacros_WithPropertyConverter_ReturnsBadRequest()
    {
        // Property converter cannot answer the macro-listing endpoint.
        var converterMock = new Mock<IPropertyConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("NC to BL"))
            .Returns(converterMock.Object);

        var result = await _controller.GetMacros("NC to BL");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task GetMacros_WithUnknownConverter_ReturnsNotFound()
    {
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Unknown"))
            .Returns((Umbraco.Community.LegacyFeatureConverter.Converters.ILegacyFeatureConverter?)null);

        var result = await _controller.GetMacros("Unknown");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDocumentTypes_WithMacroConverter_ReturnsBadRequest()
    {
        // The complement of GetMacros — macro converter doesn't answer doc-type-listing.
        var converterMock = new Mock<IMacroConverter>();
        _converterServiceMock.Setup(s => s.GetLegacyConverterByName("Macro to RTB"))
            .Returns(converterMock.Object);

        var result = await _controller.GetDocumentTypes("Macro to RTB");

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // ===== GetQueueStatus =====

    [TestMethod]
    public async Task GetQueueStatus_ReturnsJsonResult()
    {
        _queueServiceMock.Setup(s => s.GetQueueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueItem>());

        var result = await _controller.GetQueueStatus();

        Assert.IsInstanceOfType<JsonResult>(result);
    }

    [TestMethod]
    public async Task GetQueueStatus_OnlyReturnsActiveItems()
    {
        // Arrange: queue has items in every status — only Queued and Running are active
        var items = new List<QueueItem>
        {
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.Queued },
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.Running },
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.Completed },
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.CompletedWithErrors },
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.Failed },
            new() { Id = Guid.NewGuid(), Status = ConversionStatus.Cancelled },
        };

        _queueServiceMock.Setup(s => s.GetQueueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetQueueStatus();

        // Assert: result contains only the 2 active items
        var jsonResult = result as JsonResult;
        Assert.IsNotNull(jsonResult);
        var returnedItems = jsonResult.Value as IEnumerable<QueueItem>;
        Assert.IsNotNull(returnedItems);
        var list = returnedItems.ToList();
        Assert.AreEqual(2, list.Count);
        Assert.IsTrue(list.All(i => i.Status == ConversionStatus.Queued || i.Status == ConversionStatus.Running));
    }

    // ===== CancelConversion =====

    [TestMethod]
    public async Task CancelConversion_WhenSuccessful_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _queueServiceMock.Setup(s => s.CancelAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.CancelConversion(id);

        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task CancelConversion_WhenFailed_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        _queueServiceMock.Setup(s => s.CancelAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.CancelConversion(id);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // ===== GetHistory =====

    [TestMethod]
    public async Task GetHistory_ReturnsJsonResult()
    {
        _historyServiceMock.Setup(s => s.GetHistoryListAsync(
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ConversionHistory>());

        var result = await _controller.GetHistory();

        Assert.IsInstanceOfType<JsonResult>(result);
    }

    [TestMethod]
    public async Task GetHistory_ClampsInvalidParameters()
    {
        _historyServiceMock.Setup(s => s.GetHistoryListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ConversionHistory>());

        // Negative page should be clamped to 1
        await _controller.GetHistory(page: -1, pageSize: 200);

        _historyServiceMock.Verify(s => s.GetHistoryListAsync(
            1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===== GetConversionDetails =====

    [TestMethod]
    public async Task GetConversionDetails_WhenNotFound_ReturnsNotFound()
    {
        _historyServiceMock.Setup(s => s.GetHistoryAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversionHistory?)null);

        var result = await _controller.GetConversionDetails(Guid.NewGuid());

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetConversionDetails_WhenFound_ReturnsJsonResult()
    {
        var id = Guid.NewGuid();
        _historyServiceMock.Setup(s => s.GetHistoryAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionHistory { Id = id, ConverterType = "Test", Status = "Completed" });
        _historyServiceMock.Setup(s => s.GetLogEntriesAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConversionLogEntry>());

        var result = await _controller.GetConversionDetails(id);

        Assert.IsInstanceOfType<JsonResult>(result);
    }
}
