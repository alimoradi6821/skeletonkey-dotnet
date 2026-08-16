using System.Text.Json;
using Json.Schema;

namespace SkeletonKey.Conformance.Tests.Support;

internal sealed class LocatorJsonSchemaConformanceValidator
{
    private static readonly string _schemaPath = Path.Combine(RepositoryPaths.Root, "schemas", "locators", "0.1", "schema.json");
    private static readonly Lazy<JsonSchema> _schema = new(
        static () => JsonSchema.FromText(File.ReadAllText(_schemaPath)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly EvaluationOptions _options = new()
    {
        OutputFormat = OutputFormat.List,
    };

    public bool Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        EvaluationResults results = _schema.Value.Evaluate(document.RootElement, _options);
        return results.IsValid;
    }
}
