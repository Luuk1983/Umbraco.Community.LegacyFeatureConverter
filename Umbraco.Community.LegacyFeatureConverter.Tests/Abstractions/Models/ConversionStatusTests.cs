using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Models;

[TestClass]
public class ConversionStatusTests
{
    [TestMethod]
    public void AllExpectedValues_Exist()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.Queued));
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.Running));
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.Completed));
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.CompletedWithErrors));
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.Failed));
        Assert.IsTrue(Enum.IsDefined(typeof(ConversionStatus), ConversionStatus.Cancelled));
    }

    [TestMethod]
    public void EnumHas_SixValues()
    {
        var values = Enum.GetValues(typeof(ConversionStatus));
        Assert.AreEqual(6, values.Length);
    }
}
