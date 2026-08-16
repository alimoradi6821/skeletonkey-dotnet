using System.Text;
using SkeletonKey.Locators.Json;

namespace SkeletonKey.Locators.Json.Tests;

/// <summary>
/// Covers strict locator JSON serialization.
/// </summary>
public sealed class LocatorJsonSerializerTests
{
    private readonly LocatorJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies minimal locator documents deserialize.
    /// </summary>
    [Fact]
    public void DeserializesMinimalLocatorDocument()
    {
        LocatorDocument document = _serializer.Deserialize("""
            {
              "$schema": "https://schemas.skeletonkey.dev/locators/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "catalog",
              "locators": {
                "save": {
                  "strategies": [{ "kind": "test-id", "value": "save" }]
                }
              }
            }
            """);

        Assert.Equal("catalog", document.Id);
    }

    /// <summary>
    /// Verifies all strategy kinds round-trip.
    /// </summary>
    [Fact]
    public void RoundTripsAllStrategyKinds()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["target"] = new LocatorDefinition(strategies:
            [
                new LocatorStrategy("role", role: "button", name: "Save"),
                new LocatorStrategy("label", value: "Phone"),
                new LocatorStrategy("placeholder", value: "Search"),
                new LocatorStrategy("text", value: "Save", match: LocatorTextMatchMode.Contains),
                new LocatorStrategy("test-id", value: "save"),
                new LocatorStrategy("title", value: "Save contact"),
                new LocatorStrategy("alt-text", value: "Profile"),
                new LocatorStrategy("css", selector: "button.save"),
                new LocatorStrategy("xpath", selector: "//button"),
            ]),
        });

        string json = RoundTrip(document);

        Assert.Contains("\"kind\": \"xpath\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies dictionary and fallback strategy order are preserved.
    /// </summary>
    [Fact]
    public void PreservesLocatorAndFallbackOrder()
    {
        LocatorDocument document = new(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["first"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "first"), new LocatorStrategy("css", selector: ".first")]),
            ["second"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "second")]),
        });

        string json = _serializer.Serialize(document, indented: false);

        AssertInOrder(json, "\"first\"", "\"test-id\"", "\"css\"", "\"second\"");
    }

    /// <summary>
    /// Verifies canonical root and locator-definition order.
    /// </summary>
    [Fact]
    public void SerializesCanonicalPropertyOrder()
    {
        LocatorDocument document = new(
            id: "catalog",
            name: "Catalog",
            description: "Description",
            locators: new Dictionary<string, LocatorDefinition>
            {
                ["save"] = new LocatorDefinition("Save", "form", LocatorCardinality.One, [new LocatorStrategy("test-id", value: "save")]),
            });

        string json = _serializer.Serialize(document, indented: false);

        AssertInOrder(json, "\"$schema\"", "\"specVersion\"", "\"id\"", "\"name\"", "\"description\"", "\"locators\"");
        AssertInOrder(json, "\"description\":\"Save\"", "\"within\"", "\"cardinality\"", "\"strategies\"");
    }

    /// <summary>
    /// Verifies invalid strict shapes are rejected.
    /// </summary>
    [Theory]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{},\"extra\":true}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[],\"extra\":true}}}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[{\"kind\":\"test-id\",\"value\":\"x\",\"extra\":true}]}}}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[{\"kind\":1,\"value\":\"x\"}]}}}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[{\"kind\":\"unknown\",\"value\":\"x\"}]}}}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[{\"kind\":\"label\"}]}}}")]
    [InlineData("{\"specVersion\":\"0.1.0\",\"id\":\"catalog\",\"locators\":{\"a\":{\"strategies\":[{\"kind\":\"label\",\"value\":\"x\",\"value\":\"y\"}]}}}")]
    public void RejectsInvalidStrictShapes(string json)
    {
        Assert.Throws<LocatorSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies serialized JSON bytes are UTF-8 without BOM.
    /// </summary>
    [Fact]
    public void WritesUtf8WithoutBom()
    {
        string json = _serializer.Serialize(new LocatorDocument(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["save"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "save")]),
        }));

        byte[] bytes = new UTF8Encoding(false).GetBytes(json);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    /// <summary>
    /// Verifies canonical locator serialization uses LF and exactly one final newline.
    /// </summary>
    [Fact]
    public void SerializesWithLfAndSingleTrailingNewline()
    {
        string json = _serializer.Serialize(new LocatorDocument(id: "catalog", locators: new Dictionary<string, LocatorDefinition>
        {
            ["save"] = new LocatorDefinition(strategies: [new LocatorStrategy("test-id", value: "save")]),
        }));

        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.False(json.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
    }

    private string RoundTrip(LocatorDocument document)
    {
        string first = _serializer.Serialize(document);
        string second = _serializer.Serialize(_serializer.Deserialize(first));
        Assert.Equal(first, second);
        return second;
    }

    private static void AssertInOrder(string text, params string[] tokens)
    {
        int currentIndex = -1;
        foreach (string token in tokens)
        {
            int nextIndex = text.IndexOf(token, currentIndex + 1, StringComparison.Ordinal);
            Assert.True(nextIndex > currentIndex, $"Token '{token}' did not appear after index {currentIndex} in: {text}");
            currentIndex = nextIndex;
        }
    }
}
