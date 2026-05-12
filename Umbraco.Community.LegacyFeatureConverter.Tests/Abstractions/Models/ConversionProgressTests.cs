using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Models;

[TestClass]
public class ConversionProgressTests
{
    [TestMethod]
    public void PercentComplete_WhenTotalIsZero_ReturnsZero()
    {
        var progress = new ConversionProgress
        {
            ProcessedCount = 0,
            TotalCount = 0
        };

        Assert.AreEqual(0, progress.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_WhenHalfDone_Returns50()
    {
        var progress = new ConversionProgress
        {
            ProcessedCount = 5,
            TotalCount = 10
        };

        Assert.AreEqual(50, progress.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_WhenFullyDone_Returns100()
    {
        var progress = new ConversionProgress
        {
            ProcessedCount = 10,
            TotalCount = 10
        };

        Assert.AreEqual(100, progress.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_RoundsCorrectly()
    {
        var progress = new ConversionProgress
        {
            ProcessedCount = 1,
            TotalCount = 3
        };

        // 1/3 = 33.33% -> rounds to 33
        Assert.AreEqual(33, progress.PercentComplete);
    }

    [TestMethod]
    public void PercentComplete_RoundsUp_WhenAboveHalf()
    {
        var progress = new ConversionProgress
        {
            ProcessedCount = 2,
            TotalCount = 3
        };

        // 2/3 = 66.67% -> rounds to 67
        Assert.AreEqual(67, progress.PercentComplete);
    }

    [TestMethod]
    public void DefaultStatus_IsRunning()
    {
        var progress = new ConversionProgress();

        Assert.AreEqual(ConversionStatus.Running, progress.Status);
    }

    [TestMethod]
    public void QueueItemId_DefaultsToNull()
    {
        var progress = new ConversionProgress();

        Assert.IsNull(progress.QueueItemId);
    }

    [TestMethod]
    public void QueueItemId_CanBeSet()
    {
        var queueItemId = Guid.NewGuid();
        var progress = new ConversionProgress
        {
            QueueItemId = queueItemId
        };

        Assert.AreEqual(queueItemId, progress.QueueItemId);
    }
}
