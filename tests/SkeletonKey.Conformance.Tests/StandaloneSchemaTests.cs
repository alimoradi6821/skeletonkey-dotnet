using System.Text.Json;
using Json.Schema;
using SkeletonKey.Conformance.Tests.Support;

namespace SkeletonKey.Conformance.Tests;

/// <summary>Validates the proposed Standalone Export 0.1 structural schema and canonical examples.</summary>
public sealed class StandaloneSchemaTests
{
    private static readonly string _schemaPath = Path.Combine(RepositoryPaths.Root, "schemas", "standalone", "0.1", "schema.json");

    [Theory]
    [InlineData("once.execution.settings.json")]
    [InlineData("interval.execution.settings.json")]
    [InlineData("daily.execution.settings.json")]
    public void CanonicalStandaloneSettingsExamplesMatchSchema(string fileName)
    {
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(_schemaPath));
        string path = Path.Combine(RepositoryPaths.Root, "examples", "standalone", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        EvaluationResults result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, fileName + " must conform to Standalone Export settings schema 0.1.");
    }

    [Fact]
    public void SchemaRejectsUnknownScheduleProperties()
    {
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(_schemaPath));
        using JsonDocument document = JsonDocument.Parse("""
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
}
