using System.Text.Json;
using SkeletonKey.Catalog;
using SkeletonKey.Catalog.Json;
using SkeletonKey.Catalog.Validation;
using SkeletonKey.Conformance.Tests.Support;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers node catalog schema and semantic conformance fixtures.
/// </summary>
public sealed class NodeCatalogConformanceFixtureTests
{
    private static readonly string _fixtureRoot = Path.Combine(RepositoryPaths.Root, "tests", "fixtures", "node-catalog");
    private static readonly string _schemaPath = Path.Combine(RepositoryPaths.Root, "schemas", "node-catalog", "0.1", "schema.json");
    private readonly NodeCatalogManifest _manifest = NodeCatalogManifest.Load(Path.Combine(_fixtureRoot, "manifest.json"));
    private readonly NodeCatalogJsonSchemaConformanceValidator _schemaValidator = new();
    private readonly NodeCatalogJsonSerializer _serializer = new();
    private readonly NodeCatalogSemanticValidator _validator = new();

    /// <summary>
    /// Verifies every valid fixture passes schema and semantic validation.
    /// </summary>
    [Fact]
    public void EveryValidNodeCatalogFixturePassesAllLayers()
    {
        foreach (NodeCatalogCase testCase in Cases("valid"))
        {
            NodeCatalogDocument document = _serializer.Deserialize(ReadFixture(testCase));

            Assert.True(ValidateSchema(testCase));
            Assert.True(_validator.Validate(document).IsValid);
        }
    }

    /// <summary>
    /// Verifies schema-invalid fixtures fail schema validation.
    /// </summary>
    [Fact]
    public void EverySchemaInvalidNodeCatalogFixtureFailsSchemaValidation()
    {
        foreach (NodeCatalogCase testCase in Cases("schema-invalid"))
        {
            Assert.False(ValidateSchema(testCase));
        }
    }

    /// <summary>
    /// Verifies semantic-invalid fixtures pass schema but fail semantic validation with stable codes.
    /// </summary>
    [Fact]
    public void EverySemanticInvalidNodeCatalogFixtureFailsSemanticValidationOnly()
    {
        foreach (NodeCatalogCase testCase in Cases("semantic-invalid"))
        {
            NodeCatalogDocument document = _serializer.Deserialize(ReadFixture(testCase));
            NodeCatalogValidationResult result = _validator.Validate(document);

            Assert.True(ValidateSchema(testCase));
            Assert.False(result.IsValid);
            Assert.Equal(testCase.Semantic!.Errors, [.. result.Issues.Select(static issue => issue.Code)]);
        }
    }

    /// <summary>
    /// Verifies every node catalog fixture is listed exactly once.
    /// </summary>
    [Fact]
    public void EveryNodeCatalogFixtureAppearsExactlyOnceInManifest()
    {
        string[] fixtureFiles = [.. Directory.EnumerateFiles(_fixtureRoot, "*.node-catalog.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_fixtureRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
        string[] manifestFiles = [.. _manifest.Cases.Select(static testCase => testCase.File).Order(StringComparer.Ordinal)];

        Assert.Equal(fixtureFiles, manifestFiles);
        Assert.Equal(manifestFiles.Length, manifestFiles.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Verifies the node catalog schema is UTF-8 without BOM and ends with exactly one newline.
    /// </summary>
    [Fact]
    public void NodeCatalogSchemaFileHasStableEncodingAndTrailingNewline()
    {
        byte[] bytes = File.ReadAllBytes(_schemaPath);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.False(bytes.Length > 1 && bytes[^2] == (byte)'\n');
    }

    private IEnumerable<NodeCatalogCase> Cases(string category)
    {
        return _manifest.Cases.Where(testCase => string.Equals(testCase.Category, category, StringComparison.Ordinal));
    }

    private static string ReadFixture(NodeCatalogCase testCase)
    {
        return File.ReadAllText(Path.Combine(_fixtureRoot, testCase.File.Replace('/', Path.DirectorySeparatorChar)));
    }

    private bool ValidateSchema(NodeCatalogCase testCase)
    {
        return _schemaValidator.Validate(ReadFixture(testCase));
    }

    private sealed class NodeCatalogManifest
    {
        public IReadOnlyList<NodeCatalogCase> Cases { get; init; } = [];

        public static NodeCatalogManifest Load(string path)
        {
            return JsonSerializer.Deserialize<NodeCatalogManifest>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? throw new InvalidOperationException("Node catalog manifest could not be read.");
        }
    }

    private sealed class NodeCatalogCase
    {
        public string Category { get; init; } = string.Empty;

        public string File { get; init; } = string.Empty;

        public NodeCatalogSemanticExpectation? Semantic { get; init; }
    }

    private sealed class NodeCatalogSemanticExpectation
    {
        public IReadOnlyList<string> Errors { get; init; } = [];
    }
}
