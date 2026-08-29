using System.Text.Json;
using Json.Schema;
using SkeletonKey.Conformance.Tests.Support;

namespace SkeletonKey.Conformance.Tests;

/// <summary>Validates the proposed Standalone Export 0.1 structural schema and canonical examples.</summary>
public sealed class StandaloneSchemaTests
{
    private static readonly string _schemaPath = Path.Combine(RepositoryPaths.Root, "schemas", "standalone", "0.1", "schema.json");

    /// <summary>Validates a canonical standalone settings example against the Standalone Export 0.1 schema.</summary>
    /// <param name="fileName">The canonical settings file name.</param>
    [Theory]
    [InlineData("once.execution.settings.json")]
    [InlineData("interval.execution.settings.json")]
    [InlineData("daily.execution.settings.json")]
    public void CanonicalStandaloneSettingsExamplesMatchSchema(string fileName)
    {
        var schema = LoadSchema();
        string path = Path.Combine(RepositoryPaths.Root, "examples", "standalone", fileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        EvaluationResults result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, fileName + " must conform to Standalone Export settings schema 0.1.");
    }

    /// <summary>Verifies that unknown schedule properties are rejected by the standalone schema.</summary>
    [Fact]
    public void SchemaRejectsUnknownScheduleProperties()
    {
        var schema = LoadSchema();
        using var document = JsonDocument.Parse("""
            {
              "specVersion": "0.1",
              "schedule": {
                "type": "once",
                "cron": "* * * * *"
              }
            }
            """);
        EvaluationResults result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.False(result.IsValid);
    }

    private static JsonSchema LoadSchema()
    {
        var buildOptions = new BuildOptions
        {
            SchemaRegistry = new SchemaRegistry(),
        };
        return JsonSchema.FromText(File.ReadAllText(_schemaPath), buildOptions);
    }
}
