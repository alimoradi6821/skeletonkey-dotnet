using System.Text.RegularExpressions;
using SkeletonKey.Conformance.Tests.Support;

namespace SkeletonKey.Conformance.Tests;

/// <summary>
/// Covers conformance manifest integrity.
/// </summary>
public sealed partial class ManifestTests
{
    private static readonly string[] _categories =
    [
        "valid",
        "serialization-invalid",
        "schema-invalid",
        "semantic-invalid",
        "warning",
    ];

    private static readonly string[] _serializationExpectations = ["success", "failure"];
    private static readonly string[] _schemaExpectations = ["valid", "invalid", "not-applicable"];

    /// <summary>
    /// Verifies that the manifest format version is 0.1.0.
    /// </summary>
    [Fact]
    public void ManifestFormatVersionIs010()
    {
        Assert.Equal("0.1.0", ConformanceManifest.Load().FormatVersion);
    }

    /// <summary>
    /// Verifies that manifest case IDs are unique.
    /// </summary>
    [Fact]
    public void ManifestCaseIdsAreUnique()
    {
        string[] ids = [.. ConformanceManifest.Load().Cases.Select(static testCase => testCase.Id)];

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Verifies that manifest fixture paths are repository-relative.
    /// </summary>
    [Fact]
    public void ManifestFixturePathsAreRelative()
    {
        Assert.All(ConformanceManifest.Load().Cases, testCase =>
        {
            Assert.False(Path.IsPathRooted(testCase.File));
            Assert.DoesNotContain("..", testCase.File, StringComparison.Ordinal);
            Assert.DoesNotContain('\\', testCase.File);
        });
    }

    /// <summary>
    /// Verifies that every manifest fixture file exists.
    /// </summary>
    [Fact]
    public void EveryManifestFixtureFileExists()
    {
        Assert.All(ConformanceManifest.Load().Cases, testCase => Assert.True(File.Exists(testCase.FullPath), testCase.File));
    }

    /// <summary>
    /// Verifies that every fixture file appears exactly once in the manifest.
    /// </summary>
    [Fact]
    public void EveryFixtureFileAppearsExactlyOnceInManifest()
    {
        string[] manifestFiles = [.. ConformanceManifest.Load().Cases.Select(static testCase => testCase.File)];

        Assert.Equal(manifestFiles.Length, manifestFiles.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Verifies that no unlisted fixture files exist.
    /// </summary>
    [Fact]
    public void NoUnlistedFixtureFilesExist()
    {
        HashSet<string> manifestFiles = new(ConformanceManifest.Load().Cases.Select(static testCase => testCase.File), StringComparer.Ordinal);
        string[] fixtureFiles = [.. Directory.EnumerateFiles(RepositoryPaths.ConformanceRoot, "*.workflow.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryPaths.ConformanceRoot, path).Replace('\\', '/'))];

        Assert.Equal(fixtureFiles.Order(StringComparer.Ordinal), manifestFiles.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Verifies that expected validation codes use SKWxxxx format.
    /// </summary>
    [Fact]
    public void ExpectedValidationCodesUseSkwFormat()
    {
        IEnumerable<string> codes = ConformanceManifest.Load().Cases
            .Where(static testCase => testCase.Semantic is not null)
            .SelectMany(static testCase => testCase.Semantic!.Errors.Concat(testCase.Semantic.Warnings));

        Assert.All(codes, code => Assert.Matches(ValidationCodeRegex(), code));
    }

    /// <summary>
    /// Verifies that expectation values use documented enum strings.
    /// </summary>
    [Fact]
    public void ExpectationValuesUseDocumentedEnumStrings()
    {
        Assert.All(ConformanceManifest.Load().Cases, testCase =>
        {
            Assert.Contains(testCase.Category, _categories);
            Assert.Contains(testCase.Serialization, _serializationExpectations);
            Assert.Contains(testCase.Schema, _schemaExpectations);
        });
    }

    [GeneratedRegex("^SKW[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidationCodeRegex();
}
