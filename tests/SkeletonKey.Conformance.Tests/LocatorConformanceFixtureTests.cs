using System.Text.Json;
using SkeletonKey.Conformance.Tests.Support;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Json;
using SkeletonKey.Locators.Validation;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers locator document schema and semantic conformance fixtures.
/// </summary>
public sealed class LocatorConformanceFixtureTests
{
    private static readonly string _root = FindRepositoryRoot();
    private static readonly string _fixtureRoot = Path.Combine(_root, "tests", "fixtures", "locators");
    private static readonly string _schemaPath = Path.Combine(_root, "schemas", "locators", "0.1", "schema.json");
    private readonly LocatorManifest _manifest = LocatorManifest.Load(Path.Combine(_fixtureRoot, "manifest.json"));
    private readonly LocatorJsonSchemaConformanceValidator _schemaValidator = new();
    private readonly LocatorJsonSerializer _serializer = new();
    private readonly LocatorSemanticValidator _validator = new();

    /// <summary>
    /// Verifies every valid locator fixture passes all layers.
    /// </summary>
    [Fact]
    public void EveryValidLocatorFixturePassesAllLayers()
    {
        foreach (LocatorCase testCase in Cases("valid"))
        {
            LocatorDocument document = _serializer.Deserialize(ReadFixture(testCase));
            Assert.True(ValidateSchema(testCase));
            Assert.True(_validator.Validate(document).IsValid);
        }
    }

    /// <summary>
    /// Verifies schema-invalid locator fixtures fail schema validation.
    /// </summary>
    [Fact]
    public void EverySchemaInvalidLocatorFixtureFailsSchemaValidation()
    {
        foreach (LocatorCase testCase in Cases("schema-invalid"))
        {
            Assert.False(ValidateSchema(testCase));
        }
    }

    /// <summary>
    /// Verifies semantic-invalid locator fixtures pass schema but fail semantic validation with stable codes.
    /// </summary>
    [Fact]
    public void EverySemanticInvalidLocatorFixtureFailsSemanticValidationOnly()
    {
        foreach (LocatorCase testCase in Cases("semantic-invalid"))
        {
            LocatorDocument document = _serializer.Deserialize(ReadFixture(testCase));
            LocatorValidationResult result = _validator.Validate(document);

            Assert.True(ValidateSchema(testCase));
            Assert.False(result.IsValid);
            Assert.Equal(testCase.Semantic!.Errors, [.. result.Issues.Select(static issue => issue.Code)]);
        }
    }

    /// <summary>
    /// Verifies every locator fixture is listed exactly once in the manifest.
    /// </summary>
    [Fact]
    public void EveryLocatorFixtureAppearsExactlyOnceInManifest()
    {
        string[] fixtureFiles = [.. Directory.EnumerateFiles(_fixtureRoot, "*.locators.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_fixtureRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
        string[] manifestFiles = [.. _manifest.Cases.Select(static testCase => testCase.File).Order(StringComparer.Ordinal)];

        Assert.Equal(fixtureFiles, manifestFiles);
        Assert.Equal(manifestFiles.Length, manifestFiles.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Verifies the locator schema is UTF-8 without BOM and ends with exactly one newline.
    /// </summary>
    [Fact]
    public void LocatorSchemaFileHasStableEncodingAndTrailingNewline()
    {
        byte[] bytes = File.ReadAllBytes(_schemaPath);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.True(bytes.Length > 0);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.False(bytes.Length > 1 && bytes[^2] == (byte)'\n');
    }

    private IEnumerable<LocatorCase> Cases(string category)
    {
        return _manifest.Cases.Where(testCase => string.Equals(testCase.Category, category, StringComparison.Ordinal));
    }

    private static string ReadFixture(LocatorCase testCase)
    {
        return File.ReadAllText(Path.Combine(_fixtureRoot, testCase.File.Replace('/', Path.DirectorySeparatorChar)));
    }

    private bool ValidateSchema(LocatorCase testCase)
    {
        return _schemaValidator.Validate(ReadFixture(testCase));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SkeletonKey.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SkeletonKey.sln from the test output directory.");
    }

    private sealed class LocatorManifest
    {
        public IReadOnlyList<LocatorCase> Cases { get; init; } = [];

        public static LocatorManifest Load(string path)
        {
            return JsonSerializer.Deserialize<LocatorManifest>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }) ?? throw new InvalidOperationException("Locator manifest could not be read.");
        }
    }

    private sealed class LocatorCase
    {
        public string Category { get; init; } = string.Empty;

        public string File { get; init; } = string.Empty;

        public LocatorSemanticExpectation? Semantic { get; init; }
    }

    private sealed class LocatorSemanticExpectation
    {
        public IReadOnlyList<string> Errors { get; init; } = [];
    }
}
