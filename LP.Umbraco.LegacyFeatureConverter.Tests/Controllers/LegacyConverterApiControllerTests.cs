using LP.Umbraco.LegacyFeatureConverter.Controllers;
using LP.Umbraco.LegacyFeatureConverter.Converters;
using LP.Umbraco.LegacyFeatureConverter.Dtos;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Controllers;

[TestClass]
public class LegacyConverterApiControllerTests
{
    private Mock<IConverterService> _converterServiceMock = null!;
    private Mock<IConversionHistoryService> _historyServiceMock = null!;
    private Mock<IConversionQueueService> _queueServiceMock = null!;
    private Mock<ILogger<LegacyConverterApiController>> _loggerMock = null!;
    private LegacyConverterApiController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _converterServiceMock = new Mock<IConverterService>();
        _historyServiceMock = new Mock<IConversionHistoryService>();
        _queueServiceMock = new Mock<IConversionQueueService>();
        _loggerMock = new Mock<ILogger<LegacyConverterApiController>>();

        _controller = new LegacyConverterApiController(
            _converterServiceMock.Object,
            _historyServiceMock.Object,
            _queueServiceMock.Object,
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
        _converterServiceMock.Setup(s => s.GetConverterByName("Unknown"))
            .Returns((IPropertyConverter?)null);

        var result = await _controller.GetDocumentTypes("Unknown");

        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetDocumentTypes_WithValidConverter_ReturnsOk()
    {
        var converterMock = new Mock<IPropertyConverter>();
        _converterServiceMock.Setup(s => s.GetConverterByName("NC to BL"))
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
        _converterServiceMock.Setup(s => s.GetConverterByName("Unknown"))
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
        _converterServiceMock.Setup(s => s.GetConverterByName("NC to BL"))
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

    // ===== GetQueueStatus =====

    [TestMethod]
    public async Task GetQueueStatus_ReturnsJsonResult()
    {
        _queueServiceMock.Setup(s => s.GetQueueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QueueItem>());

        var result = await _controller.GetQueueStatus();

        Assert.IsInstanceOfType<JsonResult>(result);
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
