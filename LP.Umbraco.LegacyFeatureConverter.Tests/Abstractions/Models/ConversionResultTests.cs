using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Abstractions.Models;

[TestClass]
public class ConversionResultTests
{
    [TestMethod]
    public void TotalItems_WithNoItems_ReturnsZero()
    {
        var result = new ConversionResult();

        Assert.AreEqual(0, result.TotalItems);
    }

    [TestMethod]
    public void TotalItems_SumsAllCategories()
    {
        var result = new ConversionResult();
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Name = "Doc1" });
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Name = "Doc2" });
        result.DataTypes.Add(new DataTypeConversionInfo { Name = "DT1" });
        result.ContentNodes.Add(new ContentConversionInfo { Name = "Content1" });

        Assert.AreEqual(4, result.TotalItems);
    }

    [TestMethod]
    public void SuccessCount_CountsOnlySuccessfulItems()
    {
        var result = new ConversionResult();
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = true });
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = false });
        result.DataTypes.Add(new DataTypeConversionInfo { Success = true });
        result.ContentNodes.Add(new ContentConversionInfo { Success = true });
        result.ContentNodes.Add(new ContentConversionInfo { Skipped = true });

        Assert.AreEqual(3, result.SuccessCount);
    }

    [TestMethod]
    public void FailureCount_CountsItemsThatAreNotSuccessfulAndNotSkipped()
    {
        var result = new ConversionResult();
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = true });
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = false, Skipped = false });
        result.DataTypes.Add(new DataTypeConversionInfo { Success = false, Skipped = true });
        result.ContentNodes.Add(new ContentConversionInfo { Success = false, Skipped = false });

        Assert.AreEqual(2, result.FailureCount);
    }

    [TestMethod]
    public void SkippedCount_CountsOnlySkippedItems()
    {
        var result = new ConversionResult();
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Skipped = true });
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Skipped = false });
        result.DataTypes.Add(new DataTypeConversionInfo { Skipped = true });
        result.ContentNodes.Add(new ContentConversionInfo { Success = true });

        Assert.AreEqual(2, result.SkippedCount);
    }

    [TestMethod]
    public void SuccessCount_PlusFailureCount_PlusSkippedCount_EqualsTotalItems()
    {
        var result = new ConversionResult();
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = true });
        result.DocumentTypes.Add(new DocumentTypeConversionInfo { Success = false, Skipped = false });
        result.DataTypes.Add(new DataTypeConversionInfo { Skipped = true });
        result.ContentNodes.Add(new ContentConversionInfo { Success = true });
        result.ContentNodes.Add(new ContentConversionInfo { Skipped = true });

        Assert.AreEqual(result.TotalItems, result.SuccessCount + result.FailureCount + result.SkippedCount);
    }

    [TestMethod]
    public void Duration_WhenNotCompleted_ReturnsNull()
    {
        var result = new ConversionResult
        {
            StartedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        Assert.IsNull(result.Duration);
    }

    [TestMethod]
    public void Duration_WhenCompleted_ReturnsCorrectTimeSpan()
    {
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 10, 5, 30, DateTimeKind.Utc);

        var result = new ConversionResult
        {
            StartedAt = start,
            CompletedAt = end
        };

        Assert.AreEqual(TimeSpan.FromMinutes(5.5), result.Duration);
    }

    [TestMethod]
    public void DefaultStatus_IsRunning()
    {
        var result = new ConversionResult();

        Assert.AreEqual(ConversionStatus.Running, result.Status);
    }

    [TestMethod]
    public void DefaultLists_AreEmpty()
    {
        var result = new ConversionResult();

        Assert.AreEqual(0, result.DocumentTypes.Count);
        Assert.AreEqual(0, result.DataTypes.Count);
        Assert.AreEqual(0, result.ContentNodes.Count);
    }
}
