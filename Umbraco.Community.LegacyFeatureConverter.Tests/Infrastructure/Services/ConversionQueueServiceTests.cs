using Umbraco.Community.LegacyFeatureConverter.Infrastructure.Services;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Tests.Data;
using Microsoft.Extensions.Logging;
using Moq;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Infrastructure.Services;

[TestClass]
public class ConversionQueueServiceTests : DbContextTestBase
{
    private ConversionQueueService _service = null!;

    [TestInitialize]
    public void Init()
    {
        var loggerMock = new Mock<ILogger<ConversionQueueService>>();
        _service = new ConversionQueueService(DbContext, loggerMock.Object);
    }

    [TestMethod]
    public async Task Enqueue_CreatesQueueItem_ReturnsId()
    {
        var options = new ConversionOptions
        {
            ConverterType = "NC to BL",
            IsTestRun = true
        };

        var id = await _service.EnqueueAsync(options);

        Assert.AreNotEqual(Guid.Empty, id);

        var item = await _service.GetQueueItemAsync(id);
        Assert.IsNotNull(item);
        Assert.AreEqual(ConversionStatus.Queued, item.Status);
        Assert.IsTrue(item.SerializedOptions.Contains("NC to BL"));
    }

    [TestMethod]
    public async Task Dequeue_ReturnsOldestQueuedItem_FIFO()
    {
        var id1 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "First" });
        await Task.Delay(10);
        var id2 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Second" });

        var dequeued = await _service.DequeueAsync();

        Assert.IsNotNull(dequeued);
        Assert.AreEqual(id1, dequeued.Id);
        Assert.AreEqual(ConversionStatus.Running, dequeued.Status);
        Assert.IsNotNull(dequeued.StartedAt);
    }

    [TestMethod]
    public async Task Dequeue_ReturnsNull_WhenQueueEmpty()
    {
        var dequeued = await _service.DequeueAsync();

        Assert.IsNull(dequeued);
    }

    [TestMethod]
    public async Task Dequeue_SkipsRunningItems()
    {
        var id1 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "First" });
        await Task.Delay(10);
        var id2 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Second" });

        // Dequeue the first (marks it Running)
        await _service.DequeueAsync();

        // Next dequeue should return the second
        var dequeued = await _service.DequeueAsync();

        Assert.IsNotNull(dequeued);
        Assert.AreEqual(id2, dequeued.Id);
    }

    [TestMethod]
    public async Task Cancel_QueudItem_ReturnsTrue()
    {
        var id = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Test" });

        var cancelled = await _service.CancelAsync(id);

        Assert.IsTrue(cancelled);

        var item = await _service.GetQueueItemAsync(id);
        Assert.IsNotNull(item);
        Assert.AreEqual(ConversionStatus.Cancelled, item.Status);
        Assert.IsNotNull(item.CompletedAt);
    }

    [TestMethod]
    public async Task Cancel_RunningItem_ReturnsFalse()
    {
        var id = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Test" });
        await _service.DequeueAsync(); // Marks it Running

        var cancelled = await _service.CancelAsync(id);

        Assert.IsFalse(cancelled);
    }

    [TestMethod]
    public async Task Cancel_NonexistentItem_ReturnsFalse()
    {
        var cancelled = await _service.CancelAsync(Guid.NewGuid());

        Assert.IsFalse(cancelled);
    }

    [TestMethod]
    public async Task CompleteQueueItem_UpdatesStatusAndHistoryId()
    {
        var id = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Test" });
        await _service.DequeueAsync();

        var historyId = Guid.NewGuid();
        await _service.CompleteQueueItemAsync(id, ConversionStatus.Completed, historyId);

        var item = await _service.GetQueueItemAsync(id);
        Assert.IsNotNull(item);
        Assert.AreEqual(ConversionStatus.Completed, item.Status);
        Assert.AreEqual(historyId, item.ConversionHistoryId);
        Assert.IsNotNull(item.CompletedAt);
    }

    [TestMethod]
    public async Task GetQueue_ReturnsAllItems_OrderedByQueuedAt()
    {
        await _service.EnqueueAsync(new ConversionOptions { ConverterType = "C" });
        await Task.Delay(10);
        await _service.EnqueueAsync(new ConversionOptions { ConverterType = "A" });
        await Task.Delay(10);
        await _service.EnqueueAsync(new ConversionOptions { ConverterType = "B" });

        var queue = (await _service.GetQueueAsync()).ToList();

        Assert.AreEqual(3, queue.Count);
        // Ordered by QueuedAt (FIFO), not by name
        Assert.IsTrue(queue[0].QueuedAt <= queue[1].QueuedAt);
        Assert.IsTrue(queue[1].QueuedAt <= queue[2].QueuedAt);
    }

    [TestMethod]
    public async Task ResetOrphanedItems_ResetsRunningToQueued()
    {
        var id1 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Orphan1" });
        var id2 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Orphan2" });
        var id3 = await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Queued" });

        // Mark first two as Running (simulating a crash mid-processing)
        await _service.DequeueAsync();
        await _service.DequeueAsync();

        var resetCount = await _service.ResetOrphanedItemsAsync();

        Assert.AreEqual(2, resetCount);

        var item1 = await _service.GetQueueItemAsync(id1);
        var item2 = await _service.GetQueueItemAsync(id2);
        var item3 = await _service.GetQueueItemAsync(id3);

        Assert.AreEqual(ConversionStatus.Queued, item1!.Status);
        Assert.IsNull(item1.StartedAt);
        Assert.AreEqual(ConversionStatus.Queued, item2!.Status);
        Assert.AreEqual(ConversionStatus.Queued, item3!.Status);
    }

    [TestMethod]
    public async Task ResetOrphanedItems_ReturnsZero_WhenNoneRunning()
    {
        await _service.EnqueueAsync(new ConversionOptions { ConverterType = "Test" });

        var resetCount = await _service.ResetOrphanedItemsAsync();

        Assert.AreEqual(0, resetCount);
    }

    [TestMethod]
    public async Task GetQueueItemAsync_ReturnsNull_WhenNotFound()
    {
        var item = await _service.GetQueueItemAsync(Guid.NewGuid());

        Assert.IsNull(item);
    }
}
