using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Binding.Internal;
using SkeletonKey.Workflow.Bindings;

namespace SkeletonKey.Binding;

/// <summary>
/// Parses and inspects structured workflow binding declarations without evaluating them.
/// </summary>
/// <remarks>
/// The reader is stateless, deterministic, thread-safe, does not mutate supplied JSON, and does not
/// inspect inside `$literal` wrappers.
/// </remarks>
public sealed class WorkflowBindingReader
{
    private static readonly IReadOnlyList<WorkflowBindingOccurrence> _emptyOccurrences = Array.AsReadOnly(Array.Empty<WorkflowBindingOccurrence>());

    /// <summary>
    /// Determines whether a JSON value is a structured binding wrapper.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns><see langword="true" /> when the value is an object containing `$binding`.</returns>
    public bool IsBinding(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$binding");
    }

    /// <summary>
    /// Determines whether a JSON value is a literal wrapper.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns><see langword="true" /> when the value is an object containing `$literal`.</returns>
    public bool IsLiteral(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$literal");
    }

    /// <summary>
    /// Reads one structured binding wrapper.
    /// </summary>
    /// <param name="value">The JSON value containing a `$binding` wrapper.</param>
    /// <returns>The immutable binding declaration.</returns>
    /// <exception cref="WorkflowBindingFormatException">Thrown when the wrapper or binding declaration is malformed.</exception>
    public WorkflowBinding Read(JsonNode value)
    {
        return Read(value, string.Empty);
    }

    /// <summary>
    /// Finds structured binding wrappers in deterministic document order.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>Binding occurrences with JSON Pointer paths.</returns>
    /// <exception cref="WorkflowBindingFormatException">Thrown when a reserved wrapper is malformed.</exception>
    public IReadOnlyList<WorkflowBindingOccurrence> FindBindings(JsonNode? value)
    {
        if (value is null)
        {
            return _emptyOccurrences;
        }

        List<WorkflowBindingOccurrence> occurrences = [];
        FindBindings(value, string.Empty, occurrences);
        return new ReadOnlyCollection<WorkflowBindingOccurrence>(occurrences);
    }

    /// <summary>
    /// Determines whether a string is a valid read-only RFC 6901 JSON Pointer for bindings.
    /// </summary>
    /// <param name="path">The candidate JSON Pointer path.</param>
    /// <returns><see langword="true" /> when the path is valid for read bindings.</returns>
    public bool IsValidBindingPath(string path)
    {
        return JsonPointerSyntax.IsValidReadPointer(path);
    }

    private static WorkflowBinding Read(JsonNode value, string path)
    {
        if (value is not JsonObject wrapper)
        {
            throw Create(path, "Binding wrapper must be a JSON object.");
        }

        if (!wrapper.ContainsKey("$binding"))
        {
            throw Create(path, "Binding wrapper must contain `$binding`.");
        }

        if (wrapper.Count != 1)
        {
            throw Create(path, "Binding wrapper must contain exactly one `$binding` property.");
        }

        if (wrapper["$binding"] is not JsonObject binding)
        {
            throw Create(JsonPointerSyntax.Combine(path, "$binding"), "`$binding` must be a JSON object.");
        }

        RejectUnknownBindingProperties(binding, JsonPointerSyntax.Combine(path, "$binding"));

        WorkflowBindingSource source = ReadSource(binding, JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "source"));
        string bindingPath = ReadOptionalString(binding, "path", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "path")) ?? string.Empty;
        WorkflowBindingMissingBehavior onMissing = binding.TryGetPropertyValue("onMissing", out JsonNode? onMissingNode)
            ? ReadOnMissing(onMissingNode, JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "onMissing"))
            : WorkflowBindingMissingBehavior.Error;
        bool hasDefault = binding.ContainsKey("default");
        JsonNode? defaultValue = hasDefault ? binding["default"]?.DeepClone() : null;

        ValidateBindingShape(source, binding, path);
        ValidateMissingBehavior(onMissing, hasDefault, path);

        return new WorkflowBinding(
            source,
            ReadOptionalString(binding, "name", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "name")),
            ReadOptionalString(binding, "node", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "node")),
            ReadOptionalString(binding, "port", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "port")),
            ReadOptionalString(binding, "iteration", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$binding"), "iteration")),
            bindingPath,
            onMissing,
            defaultValue,
            hasDefault);
    }

    private static void FindBindings(JsonNode value, string path, List<WorkflowBindingOccurrence> occurrences)
    {
        if (value is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                JsonNode? item = array[index];
                if (item is not null)
                {
                    FindBindings(item, JsonPointerSyntax.Combine(path, index), occurrences);
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

        if (jsonObject.ContainsKey("$binding"))
        {
            occurrences.Add(new WorkflowBindingOccurrence(path, Read(value, path)));
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            if (property.Value is not null)
            {
                FindBindings(property.Value, JsonPointerSyntax.Combine(path, property.Key), occurrences);
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

    private static void RejectUnknownBindingProperties(JsonObject binding, string bindingPath)
    {
        string[] known = ["source", "name", "node", "port", "iteration", "path", "onMissing", "default"];
        foreach (KeyValuePair<string, JsonNode?> property in binding)
        {
            if (!known.Contains(property.Key, StringComparer.Ordinal))
            {
                throw Create(JsonPointerSyntax.Combine(bindingPath, property.Key), $"Unknown binding property '{property.Key}' is not allowed.");
            }
        }
    }

    private static WorkflowBindingSource ReadSource(JsonObject binding, string path)
    {
        if (!binding.TryGetPropertyValue("source", out JsonNode? value))
        {
            throw Create(path, "Binding source is required.");
        }

        string text = ReadRequiredString(value, path);
        return text switch
        {
            "input" => WorkflowBindingSource.Input,
            "variable" => WorkflowBindingSource.Variable,
            "node" => WorkflowBindingSource.Node,
            "iteration" => WorkflowBindingSource.Iteration,
            _ => throw Create(path, $"Unknown binding source '{text}'."),
        };
    }

    private static WorkflowBindingMissingBehavior ReadOnMissing(JsonNode? value, string path)
    {
        string text = ReadRequiredString(value, path);
        return text switch
        {
            "error" => WorkflowBindingMissingBehavior.Error,
            "null" => WorkflowBindingMissingBehavior.Null,
            "default" => WorkflowBindingMissingBehavior.Default,
            _ => throw Create(path, $"Unknown binding missing-value behavior '{text}'."),
        };
    }

    private static void ValidateBindingShape(WorkflowBindingSource source, JsonObject binding, string wrapperPath)
    {
        bool hasName = binding.ContainsKey("name");
        bool hasNode = binding.ContainsKey("node");
        bool hasPort = binding.ContainsKey("port");
        bool hasIteration = binding.ContainsKey("iteration");

        if (source is WorkflowBindingSource.Input or WorkflowBindingSource.Variable)
        {
            if (!hasName)
            {
                throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(wrapperPath, "$binding"), "name"), "Input and variable bindings require `name`.");
            }

            if (hasNode || hasPort || hasIteration)
            {
                throw Create(wrapperPath, "Input and variable bindings must not declare `node`, `port`, or `iteration`.");
            }
        }
        else if (source is WorkflowBindingSource.Node)
        {
            if (!hasNode || !hasPort)
            {
                throw Create(wrapperPath, "Node bindings require `node` and `port`.");
            }

            if (hasName || hasIteration)
            {
                throw Create(wrapperPath, "Node bindings must not declare `name` or `iteration`.");
            }
        }
        else if (source is WorkflowBindingSource.Iteration)
        {
            if (!hasIteration)
            {
                throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(wrapperPath, "$binding"), "iteration"), "Iteration bindings require `iteration`.");
            }

            if (hasName || hasNode || hasPort)
            {
                throw Create(wrapperPath, "Iteration bindings must not declare `name`, `node`, or `port`.");
            }
        }
    }

    private static void ValidateMissingBehavior(WorkflowBindingMissingBehavior onMissing, bool hasDefault, string wrapperPath)
    {
        if (onMissing is WorkflowBindingMissingBehavior.Default && !hasDefault)
        {
            throw Create(JsonPointerSyntax.Combine(wrapperPath, "$binding"), "`onMissing: default` requires `default`.");
        }

        if (onMissing is not WorkflowBindingMissingBehavior.Default && hasDefault)
        {
            throw Create(JsonPointerSyntax.Combine(wrapperPath, "$binding"), "`default` is allowed only when `onMissing` is `default`.");
        }
    }

    private static string? ReadOptionalString(JsonObject binding, string propertyName, string path)
    {
        return binding.TryGetPropertyValue(propertyName, out JsonNode? value) ? ReadRequiredString(value, path) : null;
    }

    private static string ReadRequiredString(JsonNode? value, string path)
    {
        if (value is null || value.GetValueKind() is not System.Text.Json.JsonValueKind.String)
        {
            throw Create(path, "Expected a JSON string.");
        }

        return value.GetValue<string>();
    }

    private static WorkflowBindingFormatException Create(string path, string message)
    {
        return new WorkflowBindingFormatException(path, message);
    }
}
