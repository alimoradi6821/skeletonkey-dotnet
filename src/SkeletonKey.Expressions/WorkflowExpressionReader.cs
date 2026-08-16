using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Expressions.Internal;

namespace SkeletonKey.Expressions;

/// <summary>
/// Inspects expression workflow-value wrappers without evaluating expression text.
/// </summary>
/// <remarks>
/// The reader is stateless, deterministic, thread-safe, does not mutate supplied JSON, and does not
/// inspect inside `$literal` wrappers.
/// </remarks>
public sealed class WorkflowExpressionReader
{
    private static readonly IReadOnlyList<WorkflowExpressionOccurrence> _emptyOccurrences = Array.AsReadOnly(Array.Empty<WorkflowExpressionOccurrence>());

    /// <summary>
    /// Determines whether a JSON value is an expression wrapper.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns><see langword="true" /> when the value is an object containing `$expression`.</returns>
    public bool IsExpression(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$expression");
    }

    /// <summary>
    /// Reads the exact expression text from one expression wrapper.
    /// </summary>
    /// <param name="value">The JSON value containing an `$expression` wrapper.</param>
    /// <returns>The exact expression text.</returns>
    /// <exception cref="WorkflowExpressionFormatException">Thrown when the wrapper is malformed.</exception>
    public string ReadText(JsonNode value)
    {
        return ReadText(value, string.Empty);
    }

    /// <summary>
    /// Finds expression wrappers in deterministic document order.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>Expression occurrences with JSON Pointer paths and exact text.</returns>
    /// <exception cref="WorkflowExpressionFormatException">Thrown when a reserved wrapper is malformed.</exception>
    public IReadOnlyList<WorkflowExpressionOccurrence> FindExpressions(JsonNode? value)
    {
        if (value is null)
        {
            return _emptyOccurrences;
        }

        List<WorkflowExpressionOccurrence> occurrences = [];
        FindExpressions(value, string.Empty, occurrences);
        return new ReadOnlyCollection<WorkflowExpressionOccurrence>(occurrences);
    }

    private static string ReadText(JsonNode value, string path)
    {
        if (value is not JsonObject wrapper)
        {
            throw Create(path, "Expression wrapper must be a JSON object.");
        }

        if (!wrapper.ContainsKey("$expression"))
        {
            throw Create(path, "Expression wrapper must contain `$expression`.");
        }

        if (wrapper.Count != 1)
        {
            throw Create(path, "Expression wrapper must contain exactly one `$expression` property.");
        }

        JsonNode? expression = wrapper["$expression"];
        if (expression is null || expression.GetValueKind() is not JsonValueKind.String)
        {
            throw Create(JsonPointerSyntax.Combine(path, "$expression"), "`$expression` must be a JSON string.");
        }

        string text = expression.GetValue<string>();
        if (text.Length == 0)
        {
            throw Create(JsonPointerSyntax.Combine(path, "$expression"), "`$expression` must not be empty.");
        }

        return text;
    }

    private static void FindExpressions(JsonNode value, string path, List<WorkflowExpressionOccurrence> occurrences)
    {
        if (value is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                JsonNode? item = array[index];
                if (item is not null)
                {
                    FindExpressions(item, JsonPointerSyntax.Combine(path, index), occurrences);
                }
            }

            return;
        }

        if (value is not JsonObject jsonObject)
        {
            return;
        }

        if (jsonObject.ContainsKey("$literal"))
        {
            ValidateLiteralWrapper(jsonObject, path);
            return;
        }

        if (jsonObject.ContainsKey("$expression"))
        {
            occurrences.Add(new WorkflowExpressionOccurrence(path, ReadText(value, path)));
            return;
        }

        if (jsonObject.ContainsKey("$binding"))
        {
            ValidateBindingWrapper(jsonObject, path);
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            if (property.Key is "$binding" or "$expression" or "$literal")
            {
                throw Create(JsonPointerSyntax.Combine(path, property.Key), $"Reserved workflow value property '{property.Key}' must be represented as its wrapper.");
            }

            if (property.Value is not null)
            {
                FindExpressions(property.Value, JsonPointerSyntax.Combine(path, property.Key), occurrences);
            }
        }
    }

    private static void ValidateLiteralWrapper(JsonObject wrapper, string path)
    {
        if (wrapper.Count != 1)
        {
            throw Create(path, "Literal wrapper must contain exactly one `$literal` property.");
        }
    }

    private static void ValidateBindingWrapper(JsonObject wrapper, string path)
    {
        if (wrapper.Count != 1)
        {
            throw Create(path, "Binding wrapper must contain exactly one `$binding` property.");
        }
    }

    private static WorkflowExpressionFormatException Create(string path, string message)
    {
        return new WorkflowExpressionFormatException(path, message);
    }
}
