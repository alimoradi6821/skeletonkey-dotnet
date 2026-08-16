using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Locators.Internal;

namespace SkeletonKey.Locators;

/// <summary>
/// Inspects `$locator` workflow-value wrappers without resolving locator catalogs.
/// </summary>
/// <remarks>
/// The reader is stateless, deterministic, thread-safe, does not mutate supplied JSON, and does not
/// inspect inside `$literal` wrappers.
/// </remarks>
public sealed class LocatorReferenceReader
{
    private static readonly IReadOnlyList<LocatorReferenceOccurrence> _emptyOccurrences = Array.AsReadOnly(Array.Empty<LocatorReferenceOccurrence>());

    /// <summary>
    /// Determines whether a JSON value contains a locator reference wrapper.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns><see langword="true" /> when the value is an object containing `$locator`.</returns>
    public bool IsLocatorReference(JsonNode? value)
    {
        return value is JsonObject jsonObject && jsonObject.ContainsKey("$locator");
    }

    /// <summary>
    /// Reads one locator reference wrapper.
    /// </summary>
    /// <param name="value">The JSON value containing a `$locator` wrapper.</param>
    /// <returns>The immutable locator reference.</returns>
    /// <exception cref="LocatorReferenceFormatException">Thrown when the wrapper is malformed.</exception>
    public LocatorReference Read(JsonNode value)
    {
        return Read(value, string.Empty);
    }

    /// <summary>
    /// Finds locator reference wrappers in deterministic document order.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>Locator reference occurrences with JSON Pointer paths.</returns>
    /// <exception cref="LocatorReferenceFormatException">Thrown when a reserved wrapper is malformed.</exception>
    public IReadOnlyList<LocatorReferenceOccurrence> FindLocatorReferences(JsonNode? value)
    {
        if (value is null)
        {
            return _emptyOccurrences;
        }

        List<LocatorReferenceOccurrence> occurrences = [];
        FindReferences(value, string.Empty, occurrences);
        return new ReadOnlyCollection<LocatorReferenceOccurrence>(occurrences);
    }

    private static LocatorReference Read(JsonNode value, string path)
    {
        if (value is not JsonObject wrapper)
        {
            throw Create(path, "Locator reference wrapper must be a JSON object.");
        }

        if (!wrapper.ContainsKey("$locator"))
        {
            throw Create(path, "Locator reference wrapper must contain `$locator`.");
        }

        if (wrapper.Count != 1)
        {
            throw Create(path, "Locator reference wrapper must contain exactly one `$locator` property.");
        }

        if (wrapper["$locator"] is not JsonObject reference)
        {
            throw Create(JsonPointerSyntax.Combine(path, "$locator"), "`$locator` must be a JSON object.");
        }

        foreach (KeyValuePair<string, JsonNode?> property in reference)
        {
            if (property.Key is not ("catalog" or "version" or "id"))
            {
                throw Create(JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$locator"), property.Key), $"Unknown locator reference property '{property.Key}' is not allowed.");
            }
        }

        string catalog = ReadRequiredString(reference, "catalog", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$locator"), "catalog"));
        string id = ReadRequiredString(reference, "id", JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$locator"), "id"));
        string? version = reference.TryGetPropertyValue("version", out JsonNode? versionNode)
            ? ReadRequiredString(versionNode, JsonPointerSyntax.Combine(JsonPointerSyntax.Combine(path, "$locator"), "version"))
            : null;

        return new LocatorReference(catalog, id, version);
    }

    private static void FindReferences(JsonNode value, string path, List<LocatorReferenceOccurrence> occurrences)
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

        if (jsonObject.ContainsKey("$locator"))
        {
            occurrences.Add(new LocatorReferenceOccurrence(path, Read(value, path)));
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
        {
            if (property.Value is not null)
            {
                FindReferences(property.Value, JsonPointerSyntax.Combine(path, property.Key), occurrences);
            }
        }
    }

    private static string ReadRequiredString(JsonObject reference, string propertyName, string path)
    {
        return reference.TryGetPropertyValue(propertyName, out JsonNode? value)
            ? ReadRequiredString(value, path)
            : throw Create(path, $"Locator reference {propertyName} is required.");
    }

    private static string ReadRequiredString(JsonNode? value, string path)
    {
        if (value is null || value.GetValueKind() is not JsonValueKind.String)
        {
            throw Create(path, "Expected a JSON string.");
        }

        return value.GetValue<string>();
    }

    private static void ValidateSinglePropertyWrapper(JsonObject wrapper, string path, string propertyName)
    {
        if (wrapper.Count != 1)
        {
            throw Create(path, $"{propertyName} wrapper must contain exactly one `{propertyName}` property.");
        }
    }

    private static LocatorReferenceFormatException Create(string path, string message)
    {
        return new LocatorReferenceFormatException(path, message);
    }
}
