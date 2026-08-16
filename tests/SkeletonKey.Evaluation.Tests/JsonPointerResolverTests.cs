using System.Text.Json.Nodes;

namespace SkeletonKey.Evaluation.Tests;

/// <summary>
/// Verifies read-only RFC 6901 JSON Pointer resolution.
/// </summary>
public sealed class JsonPointerResolverTests
{
    private readonly JsonPointerResolver _resolver = new();

    /// <summary>
    /// Resolves root, object, array, escaped slash, escaped tilde, and explicit null targets.
    /// </summary>
    [Fact]
    public void ResolvesValidPointersAndClonesResults()
    {
        JsonObject source = new()
        {
            ["plain"] = new JsonObject { ["value"] = 1 },
            ["items"] = new JsonArray("first", null),
            ["a/b~c"] = "escaped",
        };

        Assert.Equal(1, _resolver.Resolve(source, "/plain/value", "/x").Value!.GetValue<int>());
        Assert.Equal("first", _resolver.Resolve(source, "/items/0", "/x").Value!.GetValue<string>());
        Assert.Null(_resolver.Resolve(source, "/items/1", "/x").Value);
        Assert.Equal("escaped", _resolver.Resolve(source, "/a~1b~0c", "/x").Value!.GetValue<string>());

        JsonObject clone = _resolver.Resolve(source, "", "/x").Value!.AsObject();
        clone["plain"]!["value"] = 9;
        Assert.Equal(1, source["plain"]!["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Rejects invalid pointer syntax and invalid array indexes.
    /// </summary>
    [Theory]
    [InlineData("#/value", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("value", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("/a~2b", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("/items/-", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("/items/-1", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("/items/01", WorkflowValueErrorCode.InvalidJsonPointer)]
    [InlineData("/items/5", WorkflowValueErrorCode.JsonPointerTargetNotFound)]
    [InlineData("/scalar/value", WorkflowValueErrorCode.JsonPointerTargetNotFound)]
    public void RejectsInvalidPointers(string pointer, string expectedCode)
    {
        JsonObject source = new()
        {
            ["items"] = new JsonArray(1),
            ["scalar"] = true,
        };

        WorkflowValueResult result = _resolver.Resolve(source, pointer, "/x");

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }
}
