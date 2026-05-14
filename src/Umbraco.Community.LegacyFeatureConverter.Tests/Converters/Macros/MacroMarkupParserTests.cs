using Umbraco.Community.LegacyFeatureConverter.Converters.Macros;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Converters.Macros;

/// <summary>
/// Tests for <see cref="MacroMarkupParser"/> — the pure-function helpers that detect and
/// extract macro markup from rich-text values. Ported from the old project's
/// AutoBlockListMacroService regex patterns and tightened for v13 storage formats.
/// </summary>
[TestClass]
public class MacroMarkupParserTests
{
    // ===== HasMacro =====

    [TestMethod]
    public void HasMacro_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(MacroMarkupParser.HasMacro(null!));
        Assert.IsFalse(MacroMarkupParser.HasMacro(string.Empty));
    }

    [TestMethod]
    public void HasMacro_PlainHtml_ReturnsFalse()
    {
        Assert.IsFalse(MacroMarkupParser.HasMacro("<p>Hello <strong>world</strong></p>"));
    }

    [TestMethod]
    public void HasMacro_PersistedMacro_ReturnsTrue()
    {
        const string input = "<p>Before</p><?UMBRACO_MACRO macroAlias=\"myMacro\" /><p>After</p>";
        Assert.IsTrue(MacroMarkupParser.HasMacro(input));
    }

    [TestMethod]
    public void HasMacro_LowercaseTag_StillMatches()
    {
        // The regex is case-insensitive on the tag name.
        const string input = "<?umbraco_macro macroAlias=\"myMacro\" />";
        Assert.IsTrue(MacroMarkupParser.HasMacro(input));
    }

    [TestMethod]
    public void HasMacro_HtmlCommentWrappedMacro_ReturnsTrue()
    {
        // Editor-format storage (rare but documented in v13).
        const string input = "<p>Before</p><!--<?UMBRACO_MACRO macroAlias=\"x\" />--><p>After</p>";
        Assert.IsTrue(MacroMarkupParser.HasMacro(input));
    }

    // ===== GetMacroStrings =====

    [TestMethod]
    public void GetMacroStrings_ReturnsAllMatches_InOrder()
    {
        const string input =
            "<?UMBRACO_MACRO macroAlias=\"a\" />" +
            "<p>middle</p>" +
            "<?UMBRACO_MACRO macroAlias=\"b\" />";

        var matches = MacroMarkupParser.GetMacroStrings(input);

        Assert.AreEqual(2, matches.Length);
        Assert.IsTrue(matches[0].Contains("macroAlias=\"a\""));
        Assert.IsTrue(matches[1].Contains("macroAlias=\"b\""));
    }

    [TestMethod]
    public void GetMacroStrings_OnNonMatchingInput_ReturnsEmpty()
    {
        Assert.AreEqual(0, MacroMarkupParser.GetMacroStrings("just text").Length);
    }

    [TestMethod]
    public void GetMacroStrings_CapturesEntireElement()
    {
        const string input = "<?UMBRACO_MACRO macroAlias=\"x\" param1=\"y\" />";
        var matches = MacroMarkupParser.GetMacroStrings(input);

        Assert.AreEqual(1, matches.Length);
        Assert.AreEqual(input, matches[0]);
    }

    // ===== GetParametersFromMacro =====

    [TestMethod]
    public void GetParametersFromMacro_ReturnsAllAttributes()
    {
        const string input = "<?UMBRACO_MACRO macroAlias=\"myMacro\" title=\"Hello\" count=\"5\" />";

        var parameters = MacroMarkupParser.GetParametersFromMacro(input);

        Assert.AreEqual("myMacro", parameters["macroAlias"]);
        Assert.AreEqual("Hello", parameters["title"]);
        Assert.AreEqual("5", parameters["count"]);
    }

    [TestMethod]
    public void GetParametersFromMacro_AcceptsSingleQuotes()
    {
        const string input = "<?UMBRACO_MACRO macroAlias='myMacro' title='Hello' />";

        var parameters = MacroMarkupParser.GetParametersFromMacro(input);

        Assert.AreEqual("myMacro", parameters["macroAlias"]);
        Assert.AreEqual("Hello", parameters["title"]);
    }

    [TestMethod]
    public void GetParametersFromMacro_EmptyInput_ReturnsEmptyDictionary()
    {
        Assert.AreEqual(0, MacroMarkupParser.GetParametersFromMacro(string.Empty).Count);
    }

    [TestMethod]
    public void GetParametersFromMacro_MacroWithNoParams_ReturnsEmpty()
    {
        // No params, but tag matches the macro pattern.
        const string input = "<?UMBRACO_MACRO />";
        var parameters = MacroMarkupParser.GetParametersFromMacro(input);
        Assert.AreEqual(0, parameters.Count);
    }

    [TestMethod]
    public void TryGetMacroAlias_PresentAndPopulated_ReturnsTrue()
    {
        const string input = "<?UMBRACO_MACRO macroAlias=\"contactForm\" />";

        var ok = MacroMarkupParser.TryGetMacroAlias(input, out var alias);

        Assert.IsTrue(ok);
        Assert.AreEqual("contactForm", alias);
    }

    [TestMethod]
    public void TryGetMacroAlias_LegacyAliasAttribute_AlsoRecognized()
    {
        // Pre-v8 macros sometimes used `alias=` rather than `macroAlias=`.
        const string input = "<?UMBRACO_MACRO alias=\"oldMacro\" />";

        var ok = MacroMarkupParser.TryGetMacroAlias(input, out var alias);

        Assert.IsTrue(ok);
        Assert.AreEqual("oldMacro", alias);
    }

    [TestMethod]
    public void TryGetMacroAlias_MissingAlias_ReturnsFalse()
    {
        const string input = "<?UMBRACO_MACRO param1=\"x\" />";
        var ok = MacroMarkupParser.TryGetMacroAlias(input, out var alias);
        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, alias);
    }
}
