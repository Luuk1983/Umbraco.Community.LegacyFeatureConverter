using LP.Umbraco.LegacyFeatureConverter.Models;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Abstractions.Models;

[TestClass]
public class ConversionOptionsTests
{
    [TestMethod]
    public void DefaultValues_AreCorrect()
    {
        var options = new ConversionOptions();

        Assert.AreEqual(string.Empty, options.ConverterType);
        Assert.IsNull(options.SelectedDocumentTypeKeys);
        Assert.IsFalse(options.IsTestRun);
        Assert.IsFalse(options.StopOnError);
        Assert.IsFalse(options.RunTestFirst);
        Assert.AreEqual(Guid.Empty, options.PerformingUserKey);
        // PublishAfterConversion defaults to false (save-only is the safe default)
        Assert.IsFalse(options.PublishAfterConversion);
    }

    [TestMethod]
    public void SelectedDocumentTypeKeys_CanBeSetToNull()
    {
        var options = new ConversionOptions
        {
            SelectedDocumentTypeKeys = null
        };

        Assert.IsNull(options.SelectedDocumentTypeKeys);
    }

    [TestMethod]
    public void SelectedDocumentTypeKeys_CanBeSetToArray()
    {
        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();

        var options = new ConversionOptions
        {
            SelectedDocumentTypeKeys = new[] { key1, key2 }
        };

        Assert.AreEqual(2, options.SelectedDocumentTypeKeys!.Length);
        Assert.AreEqual(key1, options.SelectedDocumentTypeKeys[0]);
        Assert.AreEqual(key2, options.SelectedDocumentTypeKeys[1]);
    }
}
