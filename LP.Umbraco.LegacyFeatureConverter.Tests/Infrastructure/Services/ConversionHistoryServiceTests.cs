using LP.Umbraco.LegacyFeatureConverter.Infrastructure.Services;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Tests.Data;
using Microsoft.Extensions.Logging;
using Moq;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Infrastructure.Services;

[TestClass]
public class ConversionHistoryServiceTests : DbContextTestBase
{
    private ConversionHistoryService _service = null!;

    [TestInitialize]
    public void Init()
    {
        var loggerMock = new Mock<ILogger<ConversionHistoryService>>();
        _service = new ConversionHistoryService(DbContext, loggerMock.Object);
    }

    [TestMethod]
    public async Task StartConversion_CreatesHistoryRecord()
    {
        var conversionId = Guid.NewGuid();
        var userKey = Guid.NewGuid();

        await _service.StartConversionAsync(
            conversionId, "Test Converter", false, null, userKey);

        var history = await _service.GetHistoryAsync(conversionId);

        Assert.IsNotNull(history);
        Assert.AreEqual("Test Converter", history.ConverterType);
        Assert.AreEqual("Running", history.Status);
        Assert.IsFalse(history.IsTestRun);
        Assert.AreEqual(userKey, history.PerformingUserKey);
    }

    [TestMethod]
    public async Task StartConversion_WithSelectedDocTypes_SerializesToJson()
    {
        var conversionId = Guid.NewGuid();
        var keys = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await _service.StartConversionAsync(
            conversionId, "Test", true, keys, Guid.NewGuid());

        var history = await _service.GetHistoryAsync(conversionId);

        Assert.IsNotNull(history);
        Assert.IsNotNull(history.SelectedDocumentTypes);
        Assert.IsTrue(history.SelectedDocumentTypes.Contains(keys[0].ToString()));
        Assert.IsTrue(history.SelectedDocumentTypes.Contains(keys[1].ToString()));
    }

    [TestMethod]
    public async Task LogEntry_CreatesLogRecord()
    {
        var conversionId = Guid.NewGuid();
        await _service.StartConversionAsync(conversionId, "Test", false, null, Guid.NewGuid());

        await _service.LogEntryAsync(
            conversionId, LogLevel.Information, "DocumentType",
            "Processing HomePage", null, "HomePage", "abc-123");

        var logs = (await _service.GetLogEntriesAsync(conversionId)).ToList();

        Assert.AreEqual(1, logs.Count);
        Assert.AreEqual("Information", logs[0].Level);
        Assert.AreEqual("DocumentType", logs[0].ItemType);
        Assert.AreEqual("Processing HomePage", logs[0].Message);
        Assert.AreEqual("HomePage", logs[0].ItemName);
        Assert.AreEqual("abc-123", logs[0].ItemKey);
    }

    [TestMethod]
    public async Task LogEntries_ReturnedInChronologicalOrder()
    {
        var conversionId = Guid.NewGuid();
        await _service.StartConversionAsync(conversionId, "Test", false, null, Guid.NewGuid());

        await _service.LogEntryAsync(conversionId, LogLevel.Information, "A", "First", null);
        await Task.Delay(10); // Ensure different timestamps
        await _service.LogEntryAsync(conversionId, LogLevel.Information, "B", "Second", null);
        await Task.Delay(10);
        await _service.LogEntryAsync(conversionId, LogLevel.Information, "C", "Third", null);

        var logs = (await _service.GetLogEntriesAsync(conversionId)).ToList();

        Assert.AreEqual(3, logs.Count);
        Assert.AreEqual("First", logs[0].Message);
        Assert.AreEqual("Second", logs[1].Message);
        Assert.AreEqual("Third", logs[2].Message);
    }

    [TestMethod]
    public async Task CompleteConversion_UpdatesHistoryRecord()
    {
        var conversionId = Guid.NewGuid();
        await _service.StartConversionAsync(conversionId, "Test", false, null, Guid.NewGuid());

        var result = new ConversionResult
        {
            ConversionId = conversionId,
            Status = ConversionStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow
        };

        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Name = "Doc1", Success = true });
        result.DataTypes.Add(new DataTypeConversionInfo { Name = "DT1", Success = true });
        result.ContentNodes.Add(new ContentConversionInfo { Name = "C1", Success = true });
        result.ContentNodes.Add(new ContentConversionInfo { Name = "C2", Skipped = true });

        await _service.CompleteConversionAsync(conversionId, result);

        var history = await _service.GetHistoryAsync(conversionId);

        Assert.IsNotNull(history);
        Assert.AreEqual("Completed", history.Status);
        Assert.IsNotNull(history.CompletedAt);
        Assert.AreEqual(1, history.TotalDocumentTypes);
        Assert.AreEqual(1, history.TotalDataTypes);
        Assert.AreEqual(2, history.TotalContentNodes);
        Assert.AreEqual(3, history.SuccessCount);  // 1 doc type + 1 data type + 1 content node
        Assert.AreEqual(1, history.SkippedCount);  // 1 skipped content node
        Assert.IsNotNull(history.Summary);
    }

    [TestMethod]
    public async Task GetHistoryAsync_ReturnsNull_WhenNotFound()
    {
        var history = await _service.GetHistoryAsync(Guid.NewGuid());

        Assert.IsNull(history);
    }

    [TestMethod]
    public async Task GetHistoryListAsync_ReturnsPaged_OrderedByDateDesc()
    {
        for (int i = 0; i < 5; i++)
        {
            await _service.StartConversionAsync(
                Guid.NewGuid(), $"Converter {i}", false, null, Guid.NewGuid());
            await Task.Delay(10);
        }

        var page1 = await _service.GetHistoryListAsync(1, 2);
        var page2 = await _service.GetHistoryListAsync(2, 2);

        Assert.AreEqual(5, page1.TotalItems);
        Assert.AreEqual(2, page1.Items.Count);
        Assert.AreEqual(2, page2.Items.Count);
        Assert.AreEqual(3, page1.TotalPages);

        // Most recent first
        Assert.AreEqual("Converter 4", page1.Items[0].ConverterType);
        Assert.AreEqual("Converter 3", page1.Items[1].ConverterType);
    }

    [TestMethod]
    public async Task GetLogEntriesAsync_ReturnsEmpty_WhenNoLogs()
    {
        var logs = await _service.GetLogEntriesAsync(Guid.NewGuid());

        Assert.AreEqual(0, logs.Count());
    }

    [TestMethod]
    public async Task FullLifecycle_StartLogComplete()
    {
        var conversionId = Guid.NewGuid();

        // Start
        await _service.StartConversionAsync(conversionId, "NC to BL", true, null, Guid.NewGuid());

        // Log some entries
        await _service.LogEntryAsync(conversionId, LogLevel.Information, "Conversion", "Phase 1: Scanning", null);
        await _service.LogEntryAsync(conversionId, LogLevel.Warning, "DocumentType", "Skipping empty type", null);
        await _service.LogEntryAsync(conversionId, LogLevel.Error, "Content", "Failed", "stack trace here");

        // Complete
        var result = new ConversionResult
        {
            ConversionId = conversionId,
            Status = ConversionStatus.CompletedWithErrors,
            IsTestRun = true,
            StartedAt = DateTime.UtcNow.AddSeconds(-30),
            CompletedAt = DateTime.UtcNow
        };
        await _service.CompleteConversionAsync(conversionId, result);

        // Verify
        var history = await _service.GetHistoryAsync(conversionId);
        var logs = (await _service.GetLogEntriesAsync(conversionId)).ToList();

        Assert.IsNotNull(history);
        Assert.AreEqual("CompletedWithErrors", history.Status);
        Assert.AreEqual(3, logs.Count);
        Assert.AreEqual("Error", logs[2].Level);
    }
}
