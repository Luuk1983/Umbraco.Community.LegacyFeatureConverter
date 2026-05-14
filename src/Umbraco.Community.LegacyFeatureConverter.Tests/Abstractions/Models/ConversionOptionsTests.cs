using Umbraco.Community.LegacyFeatureConverter.Models;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Abstractions.Models;

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
        // Macro-specific defaults
        Assert.IsNull(options.SelectedMacroKeys);
        // GenerateStubPartialViews defaults to true (recommended) so macro conversions
        // produce migration scaffolding out of the box.
        Assert.IsTrue(options.GenerateStubPartialViews);
    }

    [TestMethod]
    public void SelectedMacroKeys_CanBeSetToArray()
    {
        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();

        var options = new ConversionOptions
        {
            SelectedMacroKeys = new[] { key1, key2 }
        };

        Assert.AreEqual(2, options.SelectedMacroKeys!.Length);
        Assert.AreEqual(key1, options.SelectedMacroKeys[0]);
        Assert.AreEqual(key2, options.SelectedMacroKeys[1]);
    }

    [TestMethod]
    public void MacroSettings_RoundTripViaJson()
    {
        // Forward compatibility: SerializedOptions in the queue must round-trip
        // the new fields through System.Text.Json without loss.
        var key = Guid.NewGuid();
        var original = new ConversionOptions
        {
            ConverterType = "Macro to Rich Text Block",
            SelectedMacroKeys = new[] { key },
            GenerateStubPartialViews = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<ConversionOptions>(json);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual("Macro to Rich Text Block", roundTripped!.ConverterType);
        Assert.AreEqual(1, roundTripped.SelectedMacroKeys!.Length);
        Assert.AreEqual(key, roundTripped.SelectedMacroKeys[0]);
        Assert.IsFalse(roundTripped.GenerateStubPartialViews);
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
