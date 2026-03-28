using LP.Umbraco.LegacyFeatureConverter.Converters.NestedContent;
using Newtonsoft.Json.Linq;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Converters.NestedContent;

/// <summary>
/// Tests for the static ParseJArrayToNCValues method that converts JArray to dictionaries.
/// </summary>
[TestClass]
public class ParseJArrayTests
{
    [TestMethod]
    public void ParsesSimpleStringProperties()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""textBlock"",""title"":""Hello""}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("textBlock", result[0]["ncContentTypeAlias"]);
        Assert.AreEqual("Hello", result[0]["title"]);
    }

    [TestMethod]
    public void PreservesNullValues()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""optionalField"":null}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result[0].ContainsKey("optionalField"));
        Assert.IsNull(result[0]["optionalField"]);
    }

    [TestMethod]
    public void PreservesJsonObjectsAsStrings()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""complexProp"":{""key"":""value""}}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual(@"{""key"":""value""}", result[0]["complexProp"]);
    }

    [TestMethod]
    public void PreservesJsonArraysAsStrings()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""tags"":[""a"",""b""]}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual(@"[""a"",""b""]", result[0]["tags"]);
    }

    [TestMethod]
    public void ParsesMultipleItems()
    {
        var json = JArray.Parse(@"[
            {""ncContentTypeAlias"":""block1"",""text"":""A""},
            {""ncContentTypeAlias"":""block2"",""text"":""B""}
        ]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("A", result[0]["text"]);
        Assert.AreEqual("B", result[1]["text"]);
    }

    [TestMethod]
    public void HandlesNumericValues()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""count"":42}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual("42", result[0]["count"]);
    }

    [TestMethod]
    public void HandlesBooleanValues()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""visible"":true}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual("true", result[0]["visible"]);
    }

    [TestMethod]
    public void HandlesEmptyStringValues()
    {
        var json = JArray.Parse(@"[{""ncContentTypeAlias"":""test"",""empty"":""""}]");

        var result = NestedContentConverter.ParseJArrayToNCValues(json);

        Assert.AreEqual("", result[0]["empty"]);
    }
}
