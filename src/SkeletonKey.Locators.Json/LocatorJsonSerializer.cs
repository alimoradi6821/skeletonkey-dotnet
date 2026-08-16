using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Locators.Json.Internal;

namespace SkeletonKey.Locators.Json;

/// <summary>
/// Serializes and deserializes locator documents using strict JSON and canonical property order.
/// </summary>
/// <remarks>The serializer is stateless, deterministic, thread-safe, and does not execute selectors.</remarks>
public sealed class LocatorJsonSerializer
{
    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly UTF8Encoding _utf8NoBom = new(false);

    /// <summary>
    /// Deserializes a locator JSON document.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The immutable locator document.</returns>
    /// <exception cref="LocatorSerializationException">Thrown when JSON syntax or shape is invalid.</exception>
    public LocatorDocument Deserialize(string json)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(json);
            JsonDuplicatePropertyDetector.RejectDuplicates(json);
            using var document = JsonDocument.Parse(json, _documentOptions);
            return ReadDocument(document.RootElement, string.Empty);
        }
        catch (LocatorSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LocatorSerializationException("Failed to deserialize locator JSON.", exception);
        }
    }

    /// <summary>
    /// Serializes a locator document into canonical JSON with LF line endings and exactly one final newline.
    /// </summary>
    /// <param name="document">The locator document.</param>
    /// <param name="indented">Whether to write indented JSON.</param>
    /// <returns>The canonical JSON text.</returns>
    /// <exception cref="LocatorSerializationException">Thrown when the document cannot be serialized.</exception>
    public string Serialize(LocatorDocument document, bool indented = true)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            using MemoryStream stream = new();
            using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = indented });
            WriteDocument(writer, document);
            writer.Flush();
            return NormalizeJsonText(_utf8NoBom.GetString(stream.ToArray()));
        }
        catch (Exception exception) when (exception is not LocatorSerializationException)
        {
            throw new LocatorSerializationException("Failed to serialize locator document.", exception);
        }
    }

    private static LocatorDocument ReadDocument(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["$schema", "specVersion", "id", "name", "description", "locators"]);
        return new LocatorDocument(
            ReadOptionalString(element, "$schema", Append(path, "$schema")),
            ReadRequiredString(element, "specVersion", Append(path, "specVersion")),
            ReadRequiredString(element, "id", Append(path, "id")),
            ReadOptionalString(element, "name", Append(path, "name")),
            ReadOptionalString(element, "description", Append(path, "description")),
            ReadLocators(element, path));
    }

    private static IReadOnlyDictionary<string, LocatorDefinition> ReadLocators(JsonElement element, string path)
    {
        JsonElement locatorsElement = ReadRequiredProperty(element, "locators", Append(path, "locators"));
        RequireObject(locatorsElement, Append(path, "locators"));
        Dictionary<string, LocatorDefinition> locators = new(StringComparer.Ordinal);
        foreach (JsonProperty locatorProperty in locatorsElement.EnumerateObject())
        {
            locators[locatorProperty.Name] = ReadLocatorDefinition(locatorProperty.Value, Append(Append(path, "locators"), locatorProperty.Name));
        }

        return locators;
    }

    private static LocatorDefinition ReadLocatorDefinition(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["description", "within", "cardinality", "strategies"]);
        return new LocatorDefinition(
            ReadOptionalString(element, "description", Append(path, "description")),
            ReadOptionalString(element, "within", Append(path, "within")),
            element.TryGetProperty("cardinality", out JsonElement cardinalityElement)
                ? ReadCardinality(ReadRequiredStringValue(cardinalityElement, Append(path, "cardinality")), Append(path, "cardinality"))
                : LocatorCardinality.One,
            ReadStrategies(element, path));
    }

    private static IReadOnlyList<LocatorStrategy> ReadStrategies(JsonElement element, string path)
    {
        JsonElement strategiesElement = ReadRequiredProperty(element, "strategies", Append(path, "strategies"));
        RequireArray(strategiesElement, Append(path, "strategies"));
        List<LocatorStrategy> strategies = [];
        int index = 0;
        foreach (JsonElement strategyElement in strategiesElement.EnumerateArray())
        {
            strategies.Add(ReadStrategy(strategyElement, Append(Append(path, "strategies"), index)));
            index++;
        }

        return strategies;
    }

    private static LocatorStrategy ReadStrategy(JsonElement element, string path)
    {
        RequireObject(element, path);
        string kind = ReadRequiredString(element, "kind", Append(path, "kind"));
        string[] known = kind switch
        {
            "role" => ["kind", "role", "name", "match", "caseSensitive"],
            "label" or "placeholder" or "text" or "title" or "alt-text" => ["kind", "value", "match", "caseSensitive"],
            "test-id" => ["kind", "value"],
            "css" or "xpath" => ["kind", "selector"],
            _ => throw Create($"Unknown locator strategy kind '{kind}'.", Append(path, "kind")),
        };
        RejectUnknownProperties(element, path, known);

        return new LocatorStrategy(
            kind,
            kind == "role" ? ReadRequiredString(element, "role", Append(path, "role")) : null,
            element.TryGetProperty("name", out JsonElement nameElement) ? ReadRequiredStringValue(nameElement, Append(path, "name")) : null,
            kind is "label" or "placeholder" or "text" or "test-id" or "title" or "alt-text" ? ReadRequiredString(element, "value", Append(path, "value")) : null,
            kind is "css" or "xpath" ? ReadRequiredString(element, "selector", Append(path, "selector")) : null,
            element.TryGetProperty("match", out JsonElement matchElement)
                ? ReadMatch(ReadRequiredStringValue(matchElement, Append(path, "match")), Append(path, "match"))
                : LocatorTextMatchMode.Exact,
            ReadOptionalBoolean(element, "caseSensitive", Append(path, "caseSensitive"), defaultValue: true));
    }

    private static void WriteDocument(Utf8JsonWriter writer, LocatorDocument document)
    {
        writer.WriteStartObject();
        WriteOptionalString(writer, "$schema", document.Schema);
        writer.WriteString("specVersion", document.SpecVersion);
        writer.WriteString("id", document.Id);
        WriteOptionalString(writer, "name", document.Name);
        WriteOptionalString(writer, "description", document.Description);
        writer.WritePropertyName("locators");
        writer.WriteStartObject();
        foreach (KeyValuePair<string, LocatorDefinition> locator in document.Locators)
        {
            writer.WritePropertyName(locator.Key);
            WriteLocatorDefinition(writer, locator.Value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string NormalizeJsonText(string json)
    {
        return json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n') + "\n";
    }

    private static void WriteLocatorDefinition(Utf8JsonWriter writer, LocatorDefinition definition)
    {
        writer.WriteStartObject();
        WriteOptionalString(writer, "description", definition.Description);
        WriteOptionalString(writer, "within", definition.Within);
        writer.WriteString("cardinality", WriteCardinality(definition.Cardinality));
        writer.WritePropertyName("strategies");
        writer.WriteStartArray();
        foreach (LocatorStrategy strategy in definition.Strategies)
        {
            WriteStrategy(writer, strategy);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStrategy(Utf8JsonWriter writer, LocatorStrategy strategy)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", strategy.Kind);
        if (strategy.Kind == "role")
        {
            WriteOptionalString(writer, "role", strategy.Role);
            WriteOptionalString(writer, "name", strategy.Name);
            writer.WriteString("match", WriteMatch(strategy.Match));
            writer.WriteBoolean("caseSensitive", strategy.CaseSensitive);
        }
        else if (strategy.Kind is "label" or "placeholder" or "text" or "title" or "alt-text")
        {
            WriteOptionalString(writer, "value", strategy.Value);
            writer.WriteString("match", WriteMatch(strategy.Match));
            writer.WriteBoolean("caseSensitive", strategy.CaseSensitive);
        }
        else if (strategy.Kind is "test-id")
        {
            WriteOptionalString(writer, "value", strategy.Value);
        }
        else if (strategy.Kind is "css" or "xpath")
        {
            WriteOptionalString(writer, "selector", strategy.Selector);
        }

        writer.WriteEndObject();
    }

    private static void RejectUnknownProperties(JsonElement element, string path, string[] knownProperties)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                throw Create($"Unknown property '{property.Name}' is not allowed.", Append(path, property.Name));
            }
        }
    }

    private static JsonElement ReadRequiredProperty(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            throw Create($"Required property '{propertyName}' is missing.", path);
        }

        return property;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string path)
    {
        return ReadRequiredStringValue(ReadRequiredProperty(element, propertyName, path), path);
    }

    private static string ReadRequiredStringValue(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.String)
        {
            throw Create("Expected a JSON string.", path);
        }

        return element.GetString() ?? throw Create("Required string value cannot be null.", path);
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return ReadRequiredStringValue(property, path);
    }

    private static bool ReadOptionalBoolean(JsonElement element, string propertyName, string path, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return defaultValue;
        }

        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw Create("Expected a JSON boolean.", path);
        }

        return property.GetBoolean();
    }

    private static LocatorCardinality ReadCardinality(string value, string path)
    {
        return value switch
        {
            "one" => LocatorCardinality.One,
            "zero-or-one" => LocatorCardinality.ZeroOrOne,
            "one-or-more" => LocatorCardinality.OneOrMore,
            "many" => LocatorCardinality.Many,
            _ => throw Create($"Unknown locator cardinality '{value}'.", path),
        };
    }

    private static string WriteCardinality(LocatorCardinality value)
    {
        return value switch
        {
            LocatorCardinality.One => "one",
            LocatorCardinality.ZeroOrOne => "zero-or-one",
            LocatorCardinality.OneOrMore => "one-or-more",
            LocatorCardinality.Many => "many",
            _ => throw new InvalidOperationException($"Unknown locator cardinality '{value}'."),
        };
    }

    private static LocatorTextMatchMode ReadMatch(string value, string path)
    {
        return value switch
        {
            "exact" => LocatorTextMatchMode.Exact,
            "contains" => LocatorTextMatchMode.Contains,
            _ => throw Create($"Unknown locator text match mode '{value}'.", path),
        };
    }

    private static string WriteMatch(LocatorTextMatchMode value)
    {
        return value switch
        {
            LocatorTextMatchMode.Exact => "exact",
            LocatorTextMatchMode.Contains => "contains",
            _ => throw new InvalidOperationException($"Unknown locator text match mode '{value}'."),
        };
    }

    private static void RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw Create("Expected a JSON object.", path);
        }
    }

    private static void RequireArray(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Array)
        {
            throw Create("Expected a JSON array.", path);
        }
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static string Append(string path, string token)
    {
        return path + "/" + token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    private static string Append(string path, int index)
    {
        return path + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static LocatorSerializationException Create(string message, string path)
    {
        return new LocatorSerializationException($"{message} Path '{path}'.");
    }
}
