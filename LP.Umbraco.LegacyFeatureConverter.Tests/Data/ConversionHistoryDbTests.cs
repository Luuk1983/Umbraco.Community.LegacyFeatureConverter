using LP.Umbraco.LegacyFeatureConverter.Models;
using Microsoft.EntityFrameworkCore;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Data;

[TestClass]
public class ConversionHistoryDbTests : DbContextTestBase
{
    [TestMethod]
    public async Task CanInsert_ConversionHistory()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test Converter",
            IsTestRun = false,
            Status = "Running",
            PerformingUserKey = Guid.NewGuid()
        };

        DbContext.ConversionHistories.Add(history);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionHistories.FindAsync(history.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Test Converter", loaded.ConverterType);
        Assert.AreEqual("Running", loaded.Status);
        Assert.IsFalse(loaded.IsTestRun);
    }

    [TestMethod]
    public async Task CanUpdate_ConversionHistory()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test Converter",
            IsTestRun = true,
            Status = "Running",
            PerformingUserKey = Guid.NewGuid()
        };

        DbContext.ConversionHistories.Add(history);
        await DbContext.SaveChangesAsync();

        history.Status = "Completed";
        history.CompletedAt = DateTime.UtcNow;
        history.SuccessCount = 5;
        history.FailureCount = 1;
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionHistories.FindAsync(history.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("Completed", loaded.Status);
        Assert.IsNotNull(loaded.CompletedAt);
        Assert.AreEqual(5, loaded.SuccessCount);
        Assert.AreEqual(1, loaded.FailureCount);
    }

    [TestMethod]
    public async Task DefaultCounts_AreZero()
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
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionHistories.FindAsync(history.Id);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(0, loaded.TotalDocumentTypes);
        Assert.AreEqual(0, loaded.TotalDataTypes);
        Assert.AreEqual(0, loaded.TotalContentNodes);
        Assert.AreEqual(0, loaded.SuccessCount);
        Assert.AreEqual(0, loaded.FailureCount);
        Assert.AreEqual(0, loaded.SkippedCount);
    }

    [TestMethod]
    public async Task CanStore_NullableFields()
    {
        var history = new ConversionHistory
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ConverterType = "Test",
            Status = "Running",
            PerformingUserKey = Guid.NewGuid(),
            CompletedAt = null,
            Summary = null,
            SelectedDocumentTypes = null
        };

        DbContext.ConversionHistories.Add(history);
        await DbContext.SaveChangesAsync();

        var loaded = await DbContext.ConversionHistories.FindAsync(history.Id);

        Assert.IsNotNull(loaded);
        Assert.IsNull(loaded.CompletedAt);
        Assert.IsNull(loaded.Summary);
        Assert.IsNull(loaded.SelectedDocumentTypes);
    }
}
