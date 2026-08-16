using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers the normative workflow JSON Schema artifact.
/// </summary>
public sealed class SchemaArtifactTests
{
    /// <summary>
    /// Verifies that the schema file exists at the documented repository path.
    /// </summary>
    [Fact]
    public void SchemaFileExistsAtDocumentedRepositoryPath()
    {
        Assert.True(File.Exists(RepositoryPaths.SchemaPath));
    }

    /// <summary>
    /// Verifies that the schema declares JSON Schema Draft 2020-12.
    /// </summary>
    [Fact]
    public void SchemaDeclaresDraft202012()
    {
        JsonObject schema = ReadSchemaObject();

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", schema["$schema"]?.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the schema ID matches the workflow specification URI.
    /// </summary>
    [Fact]
    public void SchemaIdMatchesWorkflowSpecificationCurrentSchemaUri()
    {
        JsonObject schema = ReadSchemaObject();

        Assert.Equal(WorkflowSpecification.CurrentSchemaUri, schema["$id"]?.GetValue<string>());
    }

    /// <summary>
    /// Verifies that the schema file is UTF-8 without a byte order mark.
    /// </summary>
    [Fact]
    public void SchemaFileIsUtf8WithoutBom()
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.SchemaPath);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        _ = JsonNode.Parse(File.ReadAllText(RepositoryPaths.SchemaPath));
    }

    /// <summary>
    /// Verifies that the schema file ends with exactly one newline.
    /// </summary>
    [Fact]
    public void SchemaFileEndsWithExactlyOneNewline()
    {
        byte[] bytes = File.ReadAllBytes(RepositoryPaths.SchemaPath);

        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.False(bytes.Length > 1 && bytes[^2] == (byte)'\n');
    }

    /// <summary>
    /// Verifies that the schema parses successfully using JsonSchema.Net.
    /// </summary>
    [Fact]
    public void SchemaParsesSuccessfullyUsingSelectedSchemaLibrary()
    {
        JsonSchema schema = JsonSchemaConformanceValidator.LoadSchema();

        Assert.NotNull(schema);
    }

    /// <summary>
    /// Verifies that schema references are local.
    /// </summary>
    [Fact]
    public void SchemaContainsNoRemoteReferences()
    {
        JsonObject schema = ReadSchemaObject();
        string[] references = [.. EnumerateProperties(schema).Where(static property => property.Key == "$ref").Select(static property => property.Value?.GetValue<string>() ?? string.Empty)];

        Assert.All(references, reference => Assert.StartsWith("#/", reference, StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies that the schema contains no implementation-specific extension keywords.
    /// </summary>
    [Fact]
    public void SchemaContainsNoImplementationSpecificExtensionKeywords()
    {
        JsonObject schema = ReadSchemaObject();
        string[] extensionKeywords = [.. EnumerateProperties(schema).Select(static property => property.Key).Where(static name => name.StartsWith("x-", StringComparison.OrdinalIgnoreCase))];

        Assert.Empty(extensionKeywords);
    }

    private static JsonObject ReadSchemaObject()
    {
        return JsonNode.Parse(File.ReadAllText(RepositoryPaths.SchemaPath))?.AsObject()
            ?? throw new InvalidOperationException("Schema root is not a JSON object.");
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> EnumerateProperties(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
            {
                yield return property;

                foreach (KeyValuePair<string, JsonNode?> child in EnumerateProperties(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                foreach (KeyValuePair<string, JsonNode?> child in EnumerateProperties(item))
                {
                    yield return child;
                }
            }
        }
    }
}
