using LP.Umbraco.LegacyFeatureConverter.Models;
using Microsoft.EntityFrameworkCore;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Data;

[TestClass]
public class QueueItemDbTests : DbContextTestBase
{
    [TestMethod]
    public async Task CanInsert_QueueItem()
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{\"converterType\":\"Test\"}",
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Queued
        };

        DbContext.QueueItems.Add(item);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.QueueItems.FindAsync(item.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(ConversionStatus.Queued, loaded.Status);
        Assert.AreEqual("{\"converterType\":\"Test\"}", loaded.SerializedOptions);
    }

    [TestMethod]
    public async Task Status_SurvivesRoundTrip_AsEnumValue()
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Running
        };

        DbContext.QueueItems.Add(item);
        await DbContext.SaveChangesAsync();

        // Detach and reload to verify the value round-trips correctly through the database
        DbContext.Entry(item).State = EntityState.Detached;
        var loaded = await DbContext.QueueItems.FindAsync(item.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(ConversionStatus.Running, loaded.Status);

        // Also verify we can query by enum value (which requires string storage to work)
        var queried = await DbContext.QueueItems
            .Where(q => q.Status == ConversionStatus.Running)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(queried);
        Assert.AreEqual(item.Id, queried.Id);
    }

    [TestMethod]
    public async Task CanUpdate_QueueItemStatus()
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Queued
        };

        DbContext.QueueItems.Add(item);
        await DbContext.SaveChangesAsync();

        item.Status = ConversionStatus.Running;
        item.StartedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.QueueItems.FindAsync(item.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(ConversionStatus.Running, loaded.Status);
        Assert.IsNotNull(loaded.StartedAt);
    }

    [TestMethod]
    public async Task CanQuery_ByStatus_ForFifoOrdering()
    {
        var item1 = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{\"order\":1}",
            QueuedAt = DateTime.UtcNow.AddMinutes(-2),
            Status = ConversionStatus.Queued
        };

        var item2 = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{\"order\":2}",
            QueuedAt = DateTime.UtcNow.AddMinutes(-1),
            Status = ConversionStatus.Queued
        };

        var item3 = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{\"order\":3}",
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Completed
        };

        DbContext.QueueItems.AddRange(item1, item2, item3);
        await DbContext.SaveChangesAsync();

        // Get oldest queued item (FIFO)
        var next = await DbContext.QueueItems
            .Where(q => q.Status == ConversionStatus.Queued)
            .OrderBy(q => q.QueuedAt)
            .FirstOrDefaultAsync();

        Assert.IsNotNull(next);
        Assert.AreEqual(item1.Id, next.Id);
        Assert.AreEqual("{\"order\":1}", next.SerializedOptions);
    }

    [TestMethod]
    public async Task CanDelete_QueueItem()
    {
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = ConversionStatus.Queued
        };

        DbContext.QueueItems.Add(item);
        await DbContext.SaveChangesAsync();

        DbContext.QueueItems.Remove(item);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.QueueItems.FindAsync(item.Id);
        Assert.IsNull(loaded);
    }

    [TestMethod]
    public async Task CanStore_CompletedQueueItem_WithHistoryId()
    {
        var historyId = Guid.NewGuid();
        var item = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{}",
            QueuedAt = DateTime.UtcNow.AddMinutes(-5),
            StartedAt = DateTime.UtcNow.AddMinutes(-4),
            CompletedAt = DateTime.UtcNow,
            Status = ConversionStatus.Completed,
            ConversionHistoryId = historyId
        };

        DbContext.QueueItems.Add(item);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.QueueItems.FindAsync(item.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(ConversionStatus.Completed, loaded.Status);
        Assert.AreEqual(historyId, loaded.ConversionHistoryId);
        Assert.IsNotNull(loaded.StartedAt);
        Assert.IsNotNull(loaded.CompletedAt);
    }
}
