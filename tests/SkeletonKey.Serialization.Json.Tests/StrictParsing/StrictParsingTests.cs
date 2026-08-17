using System.Text.Json.Nodes;

namespace SkeletonKey.Serialization.Json.Tests.StrictParsing;

/// <summary>
/// Covers workflow JSON serialization behavior.
/// </summary>
public sealed class StrictParsingTests
{
    private readonly WorkflowJsonSerializer _serializer = new();

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Theory]
    [InlineData("\"unexpected\": true", "Rejects unknown root property")]
    [InlineData("\"specversion\": \"0.1.0\"", "Rejects incorrect property casing")]
    public void RejectsUnknownOrIncorrectRootProperty(string extraProperty, string _)
    {
        string json = $$"""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [],
              {{extraProperty}}
            }
            """;

        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsUnknownNodeProperty()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "type": "core.start", "typeVersion": 1, "unexpected": true }],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsUnknownEndpointProperty()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [{ "from": { "node": "a", "port": "main", "extra": true }, "to": { "node": "b", "port": "main" } }]
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsComments()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              // no comments
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsTrailingCommas()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [],
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Theory]
    [InlineData("{ \"id\": \"first\", \"id\": \"second\" }")]
    [InlineData("{ \"nodes\": [{ \"id\": \"a\", \"id\": \"b\" }] }")]
    [InlineData("{ \"parameters\": { \"message\": \"a\", \"message\": \"b\" } }")]
    [InlineData("{ \"variables\": { \"item\": { \"value\": 1, \"value\": 2 } } }")]
    public void RejectsDuplicatePropertiesAnywhere(string json)
    {
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsNumericEnumValue()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "inputs": { "name": { "type": 1 } },
              "nodes": [],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsUnknownEnumText()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "inputs": { "name": { "type": "text" } },
              "nodes": [],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Theory]
    [InlineData("$schema")]
    [InlineData("specVersion")]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("nodes")]
    [InlineData("connections")]
    public void RejectsMissingRequiredRootProperty(string propertyName)
    {
        JsonObject json = JsonNode.Parse("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": []
            }
            """)!.AsObject();

        Assert.True(json.Remove(propertyName));

        AssertInvalid(json.ToJsonString());
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsMissingRequiredNodeProperty()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [{ "id": "start", "typeVersion": 1 }],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsMissingRequiredEndpointProperty()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [{ "from": { "node": "a" }, "to": { "node": "b", "port": "main" } }]
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Theory]
    [InlineData("nodes", "null")]
    [InlineData("connections", "null")]
    [InlineData("inputs", "null")]
    [InlineData("variables", "null")]
    public void RejectsNullCollections(string propertyName, string value)
    {
        string json = $$"""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [],
              "{{propertyName}}": {{value}}
            }
            """;

        AssertInvalid(json);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsNullNodeEntry()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [null],
              "connections": []
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsNullConnectionEntry()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": "minimal",
              "name": "Minimal workflow",
              "nodes": [],
              "connections": [null]
            }
            """);
    }

    /// <summary>
    /// Verifies the behavior named by this test.
    /// </summary>
    [Fact]
    public void RejectsNullRequiredString()
    {
        AssertInvalid("""
            {
              "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
              "specVersion": "0.1.0",
              "id": null,
              "name": "Minimal workflow",
              "nodes": [],
              "connections": []
            }
            """);
    }

    private void AssertInvalid(string json)
    {
        Assert.Throws<WorkflowSerializationException>(() => _serializer.Deserialize(json));
    }
}
