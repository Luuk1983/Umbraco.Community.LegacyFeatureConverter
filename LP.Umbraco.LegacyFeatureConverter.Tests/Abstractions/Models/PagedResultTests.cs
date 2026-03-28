using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Abstractions.Models;

[TestClass]
public class PagedResultTests
{
    [TestMethod]
    public void TotalPages_WithExactDivision_ReturnsCorrectCount()
    {
        var result = new PagedResult<string>
        {
            TotalItems = 20,
            PageSize = 10
        };

        Assert.AreEqual(2, result.TotalPages);
    }

    [TestMethod]
    public void TotalPages_WithRemainder_RoundsUp()
    {
        var result = new PagedResult<string>
        {
            TotalItems = 21,
            PageSize = 10
        };

        Assert.AreEqual(3, result.TotalPages);
    }

    [TestMethod]
    public void TotalPages_WithZeroItems_ReturnsZero()
    {
        var result = new PagedResult<string>
        {
            TotalItems = 0,
            PageSize = 10
        };

        Assert.AreEqual(0, result.TotalPages);
    }

    [TestMethod]
    public void TotalPages_WithZeroPageSize_ReturnsZero()
    {
        var result = new PagedResult<string>
        {
            TotalItems = 10,
            PageSize = 0
        };

        Assert.AreEqual(0, result.TotalPages);
    }

    [TestMethod]
    public void TotalPages_WithSingleItem_ReturnsOne()
    {
        var result = new PagedResult<string>
        {
            TotalItems = 1,
            PageSize = 10
        };

        Assert.AreEqual(1, result.TotalPages);
    }

    [TestMethod]
    public void DefaultItems_IsEmptyList()
    {
        var result = new PagedResult<string>();

        Assert.IsNotNull(result.Items);
        Assert.AreEqual(0, result.Items.Count);
    }
}
