using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SkeletonKey.Resources.Internal;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Resources;

/// <summary>
/// Inspects `$resource` workflow-value wrappers without resolving resources.
/// </summary>
/// <remarks>
/// The reader is stateless, deterministic, thread-safe, does not mutate supplied JSON, does not access
/// host services, and does not inspect inside `$literal` wrappers.
/// </remarks>
public sealed partial class WorkflowResourceReferenceReader
{
    private static readonly IReadOnlyList<WorkflowResourceReferenceOccurrence> _emptyOccurrences = Array.AsReadOnly(Array.Empty<WorkflowResourceReferenceOccurrence>());

    /// <summary>
    /// Determines whether a JSON value contains a resource reference wrapper.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns><see langword="true" /> when the value is an object containing `$resource`.</returns>
    public bool IsResourceReference(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$resource");
    }

    /// <summary>
    /// Reads one resource reference wrapper.
    /// </summary>
    /// <param name="value">The JSON value containing a `$resource` wrapper.</param>
    /// <returns>The immutable resource reference.</returns>
    /// <exception cref="WorkflowResourceReferenceFormatException">Thrown when the wrapper is malformed.</exception>
    public WorkflowResourceReference Read(JsonNode value)
    {
        return Read(value, string.Empty);
    }

    /// <summary>
    /// Finds resource reference wrappers in deterministic document order.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>Resource reference occurrences with JSON Pointer paths.</returns>
    /// <exception cref="WorkflowResourceReferenceFormatException">Thrown when a reserved wrapper is malformed.</exception>
    public IReadOnlyList<WorkflowResourceReferenceOccurrence> FindResourceReferences(JsonNode? value)
    {
        if (value is null)
        {
            return _emptyOccurrences;
        }

        List<WorkflowResourceReferenceOccurrence> occurrences = [];
        FindReferences(value, string.Empty, occurrences);
        return new ReadOnlyCollection<WorkflowResourceReferenceOccurrence>(occurrences);
    }

    private static WorkflowResourceReference Read(JsonNode value, string path)
    {
        if (value is not JsonObject wrapper)
        {
            throw Create(path, "Resource reference wrapper must be a JSON object.");
        }

        if (!wrapper.ContainsKey("$resource"))
        {
            throw Create(path, "Resource reference wrapper must contain `$resource`.");
        }

        if (wrapper.Count != 1)
        {
            throw Create(path, "Resource reference wrapper must contain exactly one `$resource` property.");
        }

        if (wrapper["$resource"] is not JsonObject reference)
        {
            throw Create(JsonPointerSyntax.Combine(path, "$resource"), "`$resource` must be a JSON object.");
        }

        foreach (KeyValuePair<string, JsonNode?> property in reference)
        {
            if (property.Key != "name")
            {
                throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$resource"), property.Key), $"Unknown resource reference property '{property.Key}' is not allowed.");
            }
        }

        if (!reference.TryGetPropertyValue("name", out JsonNode? nameNode) ||
            nameNode is null ||
            nameNode.GetValueKind() is not JsonValueKind.String)
        {
            throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$resource"), "name"), "Resource reference name is required.");
        }

        string name = nameNode.GetValue<string>();
        if (!ResourceNameRegex().IsMatch(name))
        {
            throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$resource"), "name"), "Resource reference name has an invalid format.");
        }

        return new WorkflowResourceReference(name);
    }

    private static void FindReferences(JsonNode value, string path, List<WorkflowResourceReferenceOccurrence> occurrences)
    {
        if (value is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                JsonNode? item = array[index];
                if (item is not null)
                {
                    FindReferences(item, JsonPointerSyntax.Combine(path, index), occurrences);
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
            ValidateSinglePropertyWrapper(jsonObject, path, "$literal");
            return;
        }

        if (jsonObject.ContainsKey("$resource"))
        {
            occurrences.Add(new WorkflowResourceReferenceOccurrence(path, Read(value, path)));
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            if (property.Key is "$resource" or "$literal")
            {
                throw Create(JsonPointerSyntax.Combine(path, property.Key), $"Reserved workflow value property '{property.Key}' must be represented as its wrapper.");
            }

            if (property.Value is not null)
            {
                FindReferences(property.Value, JsonPointerSyntax.Combine(path, property.Key), occurrences);
            }
        }
    }

    private static void ValidateSinglePropertyWrapper(JsonObject wrapper, string path, string propertyName)
    {
        if (wrapper.Count != 1)
        {
            throw Create(path, $"{propertyName} wrapper must contain exactly one `{propertyName}` property.");
        }
    }

    private static WorkflowResourceReferenceFormatException Create(string path, string message)
    {
        return new WorkflowResourceReferenceFormatException(path, message);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ResourceNameRegex();
}
