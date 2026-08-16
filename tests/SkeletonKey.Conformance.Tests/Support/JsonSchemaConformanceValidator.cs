using System.Text.Json;
using Json.Schema;

namespace SkeletonKey.Conformance.Tests.Support;

internal sealed class JsonSchemaConformanceValidator
{
    private static readonly Lazy<JsonSchema> _schema = new(
        static () => JsonSchema.FromText(File.ReadAllText(RepositoryPaths.SchemaPath)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly EvaluationOptions _options = new()
    {
        OutputFormat = OutputFormat.List,
    };

    public JsonSchemaConformanceValidator(string schemaPath)
    {
        if (!string.Equals(Path.GetFullPath(schemaPath), Path.GetFullPath(RepositoryPaths.SchemaPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only the repository Workflow 0.1 schema is supported by this test adapter.", nameof(schemaPath));
        }
    }

    public static JsonSchema LoadSchema()
    {
        return _schema.Value;
    }

    public SchemaValidationResult Validate(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            EvaluationResults results = _schema.Value.Evaluate(document.RootElement, _options);
            return new SchemaValidationResult(
                isApplicable: true,
                isValid: results.IsValid,
                diagnostics: [.. CreateDiagnostics(results)]);
        }
        catch (JsonException)
        {
            return new SchemaValidationResult(isApplicable: false, isValid: false, diagnostics: []);
        }
    }

    private static IEnumerable<SchemaValidationDiagnostic> CreateDiagnostics(EvaluationResults results)
    {
        foreach (EvaluationResults result in Flatten(results))
        {
            if (!result.IsValid)
            {
                yield return new SchemaValidationDiagnostic(
                    result.InstanceLocation.ToString(),
                    result.SchemaLocation.ToString());
            }
        }
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults result)
    {
        yield return result;

        if (result.Details is null)
        {
            yield break;
        }

        foreach (EvaluationResults detail in result.Details)
        {
            foreach (EvaluationResults flattened in Flatten(detail))
            {
                yield return flattened;
            }
        }
    }
}

internal sealed class SchemaValidationResult
{
    public SchemaValidationResult(
        bool isApplicable,
        bool isValid,
        IReadOnlyList<SchemaValidationDiagnostic> diagnostics)
    {
        IsApplicable = isApplicable;
        IsValid = isValid;
        Diagnostics = diagnostics;
    }

    public bool IsApplicable { get; }

    public bool IsValid { get; }

    public IReadOnlyList<SchemaValidationDiagnostic> Diagnostics { get; }
}

internal sealed class SchemaValidationDiagnostic
{
    public SchemaValidationDiagnostic(string instanceLocation, string schemaLocation)
    {
        InstanceLocation = instanceLocation;
        SchemaLocation = schemaLocation;
    }

    public string InstanceLocation { get; }

    public string SchemaLocation { get; }
}
