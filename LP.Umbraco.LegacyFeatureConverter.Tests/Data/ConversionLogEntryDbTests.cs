using LP.Umbraco.LegacyFeatureConverter.Models;
using Microsoft.EntityFrameworkCore;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Data;

[TestClass]
public class ConversionLogEntryDbTests : DbContextTestBase
{
    [TestMethod]
    public async Task CanInsert_LogEntryWithHistory()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test",
            Status = "Running",
            PerformingUserKey = Guid.NewGuid()
        };

        var logEntry = new ConversionLogEntry
        {
            Id = Guid.NewGuid(),
            ConversionHistoryId = history.Id,
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            ItemType = "DocumentType",
            Message = "Processing document type 'HomePage'"
        };

        DbContext.ConversionHistories.Add(history);
        DbContext.ConversionLogs.Add(logEntry);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionLogs.FindAsync(logEntry.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Information", loaded.Level);
        Assert.AreEqual("DocumentType", loaded.ItemType);
        Assert.AreEqual(history.Id, loaded.ConversionHistoryId);
    }

    [TestMethod]
    public async Task CascadeDelete_RemovesLogEntries_WhenHistoryDeleted()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test",
            Status = "Completed",
            PerformingUserKey = Guid.NewGuid()
        };

        var log1 = new ConversionLogEntry
        {
            Id = Guid.NewGuid(),
            ConversionHistoryId = history.Id,
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            ItemType = "Conversion",
            Message = "Started"
        };

        var log2 = new ConversionLogEntry
        {
            Id = Guid.NewGuid(),
            ConversionHistoryId = history.Id,
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            ItemType = "Conversion",
            Message = "Completed"
        };

        DbContext.ConversionHistories.Add(history);
        DbContext.ConversionLogs.AddRange(log1, log2);
        await DbContext.SaveChangesAsync();

        // Verify logs exist
        Assert.AreEqual(2, await DbContext.ConversionLogs.CountAsync(l => l.ConversionHistoryId == history.Id));

        // Delete the history record
        DbContext.ConversionHistories.Remove(history);
        await DbContext.SaveChangesAsync();

        // Verify cascade deleted logs
        Assert.AreEqual(0, await DbContext.ConversionLogs.CountAsync(l => l.ConversionHistoryId == history.Id));
    }

    [TestMethod]
    public async Task LogEntries_LoadedViaNavigationProperty()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test",
            Status = "Running",
            PerformingUserKey = Guid.NewGuid()
        };

        DbContext.ConversionHistories.Add(history);
        DbContext.ConversionLogs.Add(new ConversionLogEntry
        {
            Id = Guid.NewGuid(),
            ConversionHistoryId = history.Id,
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            ItemType = "Conversion",
            Message = "Test log"
        });
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionHistories
            .Include(h => h.LogEntries)
            .FirstAsync(h => h.Id == history.Id);

        Assert.AreEqual(1, loaded.LogEntries.Count);
        Assert.AreEqual("Test log", loaded.LogEntries.First().Message);
    }

    [TestMethod]
    public async Task CanStore_OptionalFields()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test",
            Status = "Running",
            PerformingUserKey = Guid.NewGuid()
        };

        var logEntry = new ConversionLogEntry
        {
            Id = Guid.NewGuid(),
            ConversionHistoryId = history.Id,
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            ItemType = "Content",
            ItemName = "Home Page",
            ItemKey = "abc-123",
            Message = "Failed to convert",
            Details = "{\"error\": \"details\"}",
            StackTrace = "at SomeMethod() line 42"
        };

        DbContext.ConversionHistories.Add(history);
        DbContext.ConversionLogs.Add(logEntry);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionLogs.FindAsync(logEntry.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Home Page", loaded.ItemName);
        Assert.AreEqual("abc-123", loaded.ItemKey);
        Assert.AreEqual("{\"error\": \"details\"}", loaded.Details);
        Assert.AreEqual("at SomeMethod() line 42", loaded.StackTrace);
    }
}
