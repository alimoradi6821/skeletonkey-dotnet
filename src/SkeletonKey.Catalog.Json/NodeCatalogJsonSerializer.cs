using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Catalog.Json.Internal;
using SkeletonKey.Locators;

namespace SkeletonKey.Catalog.Json;

/// <summary>
/// Serializes and deserializes node catalog documents using strict JSON and canonical property order.
/// </summary>
public sealed class NodeCatalogJsonSerializer
{
    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly UTF8Encoding _utf8NoBom = new(false);

    /// <summary>
    /// Deserializes a node catalog JSON document.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The immutable node catalog document.</returns>
    public NodeCatalogDocument Deserialize(string json)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(json);
            JsonDuplicatePropertyDetector.RejectDuplicates(json);
            using var document = JsonDocument.Parse(json, _documentOptions);
            return ReadDocument(document.RootElement, string.Empty);
        }
        catch (NodeCatalogSerializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new NodeCatalogSerializationException("Failed to deserialize node catalog JSON.", exception);
        }
    }

    /// <summary>
    /// Serializes a node catalog document into canonical UTF-8-compatible JSON text with exactly one trailing LF.
    /// </summary>
    /// <param name="document">The node catalog document.</param>
    /// <param name="indented">Whether to write indented JSON.</param>
    /// <returns>Canonical JSON text ending with one LF.</returns>
    public string Serialize(NodeCatalogDocument document, bool indented = true)
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
        catch (Exception exception) when (exception is not NodeCatalogSerializationException)
        {
            throw new NodeCatalogSerializationException("Failed to serialize node catalog document.", exception);
        }
    }

    private static NodeCatalogDocument ReadDocument(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["$schema", "specVersion", "id", "version", "name", "description", "definitions"]);
        return new NodeCatalogDocument(
            ReadOptionalString(element, "$schema", Append(path, "$schema")) ?? NodeCatalogSpecification.CurrentSchemaUri,
            ReadRequiredString(element, "specVersion", Append(path, "specVersion")),
            ReadRequiredString(element, "id", Append(path, "id")),
            ReadRequiredString(element, "version", Append(path, "version")),
            ReadOptionalString(element, "name", Append(path, "name")),
            ReadOptionalString(element, "description", Append(path, "description")),
            ReadDefinitions(element, path));
    }

    private static IReadOnlyList<WorkflowNodeDefinition> ReadDefinitions(JsonElement element, string path)
    {
        JsonElement definitionsElement = ReadRequiredProperty(element, "definitions", Append(path, "definitions"));
        RequireArray(definitionsElement, Append(path, "definitions"));
        List<WorkflowNodeDefinition> definitions = [];
        int index = 0;
        foreach (JsonElement definitionElement in definitionsElement.EnumerateArray())
        {
            definitions.Add(ReadDefinition(definitionElement, Append(Append(path, "definitions"), index)));
            index++;
        }

        return definitions;
    }

    private static WorkflowNodeDefinition ReadDefinition(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(
            element,
            path,
            ["type", "typeVersion", "displayName", "description", "category", "stability", "capabilities", "behavior", "deprecation", "parametersSchema", "parameterExamples", "inputs", "outputs", "dynamicPorts", "resources", "locators"]);

        return new WorkflowNodeDefinition(
            ReadRequiredString(element, "type", Append(path, "type")),
            ReadRequiredInt(element, "typeVersion", Append(path, "typeVersion")),
            ReadOptionalString(element, "displayName", Append(path, "displayName")),
            ReadOptionalString(element, "description", Append(path, "description")),
            ReadOptionalString(element, "category", Append(path, "category")),
            ReadOptionalObject(element, "parametersSchema", Append(path, "parametersSchema")),
            ReadPorts(element, "inputs", WorkflowPortDirection.Input, path),
            ReadPorts(element, "outputs", WorkflowPortDirection.Output, path),
            ReadDynamicPorts(element, path),
            ReadResources(element, path),
            ReadStringArray(element, "capabilities", Append(path, "capabilities")),
            ReadBehavior(element, path),
            ReadStability(ReadOptionalString(element, "stability", Append(path, "stability")) ?? "preview", Append(path, "stability")),
            ReadDeprecation(element, path),
            ReadExamples(element, path),
            ReadLocators(element, path));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> ReadPorts(JsonElement element, string propertyName, WorkflowPortDirection impliedDirection, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement portsElement))
        {
            return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal);
        }

        RequireObject(portsElement, Append(path, propertyName));
        Dictionary<string, WorkflowPortDefinition> ports = new(StringComparer.Ordinal);
        foreach (JsonProperty portProperty in portsElement.EnumerateObject())
        {
            ports[portProperty.Name] = ReadPort(portProperty.Name, portProperty.Value, impliedDirection, Append(Append(path, propertyName), portProperty.Name));
        }

        return ports;
    }

    private static WorkflowPortDefinition ReadPort(string name, JsonElement element, WorkflowPortDirection impliedDirection, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["direction", "required", "allowsMultiple", "valueType", "schema", "description"]);
        WorkflowPortDirection direction = element.TryGetProperty("direction", out JsonElement directionElement)
            ? ReadDirection(ReadRequiredStringValue(directionElement, Append(path, "direction")), Append(path, "direction"))
            : impliedDirection;

        return new WorkflowPortDefinition(
            name,
            direction,
            ReadOptionalBoolean(element, "required", Append(path, "required"), false),
            ReadOptionalBoolean(element, "allowsMultiple", Append(path, "allowsMultiple"), false),
            ReadOptionalString(element, "valueType", Append(path, "valueType")),
            ReadOptionalObject(element, "schema", Append(path, "schema")),
            ReadOptionalString(element, "description", Append(path, "description")));
    }

    private static IReadOnlyList<WorkflowDynamicPortRule> ReadDynamicPorts(JsonElement element, string path)
    {
        if (!element.TryGetProperty("dynamicPorts", out JsonElement dynamicPortsElement))
        {
            return [];
        }

        RequireArray(dynamicPortsElement, Append(path, "dynamicPorts"));
        List<WorkflowDynamicPortRule> rules = [];
        int index = 0;
        foreach (JsonElement ruleElement in dynamicPortsElement.EnumerateArray())
        {
            string rulePath = Append(Append(path, "dynamicPorts"), index);
            RequireObject(ruleElement, rulePath);
            RejectUnknownProperties(ruleElement, rulePath, ["kind", "direction", "sourcePointer", "idPointer", "description"]);
            rules.Add(new WorkflowDynamicPortRule(
                ReadDynamicPortKind(ReadRequiredString(ruleElement, "kind", Append(rulePath, "kind")), Append(rulePath, "kind")),
                ReadDirection(ReadRequiredString(ruleElement, "direction", Append(rulePath, "direction")), Append(rulePath, "direction")),
                ReadRequiredString(ruleElement, "sourcePointer", Append(rulePath, "sourcePointer")),
                ReadRequiredString(ruleElement, "idPointer", Append(rulePath, "idPointer")),
                ReadOptionalString(ruleElement, "description", Append(rulePath, "description"))));
            index++;
        }

        return rules;
    }

    private static IReadOnlyDictionary<string, WorkflowNodeResourceRequirement> ReadResources(JsonElement element, string path)
    {
        if (!element.TryGetProperty("resources", out JsonElement resourcesElement))
        {
            return new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal);
        }

        RequireObject(resourcesElement, Append(path, "resources"));
        Dictionary<string, WorkflowNodeResourceRequirement> resources = new(StringComparer.Ordinal);
        foreach (JsonProperty resourceProperty in resourcesElement.EnumerateObject())
        {
            string resourcePath = Append(Append(path, "resources"), resourceProperty.Name);
            JsonElement resourceElement = resourceProperty.Value;
            RequireObject(resourceElement, resourcePath);
            RejectUnknownProperties(resourceElement, resourcePath, ["kind", "required", "capabilities", "description"]);
            resources[resourceProperty.Name] = new WorkflowNodeResourceRequirement(
                resourceProperty.Name,
                ReadRequiredString(resourceElement, "kind", Append(resourcePath, "kind")),
                ReadOptionalBoolean(resourceElement, "required", Append(resourcePath, "required"), true),
                ReadStringArray(resourceElement, "capabilities", Append(resourcePath, "capabilities")),
                ReadOptionalString(resourceElement, "description", Append(resourcePath, "description")));
        }

        return resources;
    }

    private static IReadOnlyDictionary<string, NodeLocatorSlotDefinition> ReadLocators(JsonElement element, string path)
    {
        if (!element.TryGetProperty("locators", out JsonElement locatorsElement))
        {
            return new Dictionary<string, NodeLocatorSlotDefinition>(StringComparer.Ordinal);
        }

        RequireObject(locatorsElement, Append(path, "locators"));
        Dictionary<string, NodeLocatorSlotDefinition> locators = new(StringComparer.Ordinal);
        foreach (JsonProperty locatorProperty in locatorsElement.EnumerateObject())
        {
            string locatorPath = Append(Append(path, "locators"), locatorProperty.Name);
            JsonElement locatorElement = locatorProperty.Value;
            RequireObject(locatorElement, locatorPath);
            RejectUnknownProperties(locatorElement, locatorPath, ["parameterPointer", "required", "usage", "acceptedCardinalities", "description"]);
            locators[locatorProperty.Name] = new NodeLocatorSlotDefinition(
                locatorProperty.Name,
                ReadRequiredString(locatorElement, "parameterPointer", Append(locatorPath, "parameterPointer")),
                ReadOptionalBoolean(locatorElement, "required", Append(locatorPath, "required"), true),
                ReadLocatorUsage(ReadOptionalString(locatorElement, "usage", Append(locatorPath, "usage")) ?? "single", Append(locatorPath, "usage")),
                ReadLocatorCardinalities(locatorElement, locatorPath),
                ReadOptionalString(locatorElement, "description", Append(locatorPath, "description")));
        }

        return locators;
    }

    private static WorkflowNodeBehaviorMetadata ReadBehavior(JsonElement element, string path)
    {
        if (!element.TryGetProperty("behavior", out JsonElement behaviorElement))
        {
            return new WorkflowNodeBehaviorMetadata();
        }

        string behaviorPath = Append(path, "behavior");
        RequireObject(behaviorElement, behaviorPath);
        RejectUnknownProperties(behaviorElement, behaviorPath, ["kind", "terminal", "maySuspend", "description"]);
        return new WorkflowNodeBehaviorMetadata(
            ReadBehaviorKind(ReadOptionalString(behaviorElement, "kind", Append(behaviorPath, "kind")) ?? "action", Append(behaviorPath, "kind")),
            ReadOptionalBoolean(behaviorElement, "terminal", Append(behaviorPath, "terminal"), false),
            ReadOptionalBoolean(behaviorElement, "maySuspend", Append(behaviorPath, "maySuspend"), false),
            ReadOptionalString(behaviorElement, "description", Append(behaviorPath, "description")));
    }

    private static WorkflowNodeDeprecationMetadata ReadDeprecation(JsonElement element, string path)
    {
        if (!element.TryGetProperty("deprecation", out JsonElement deprecationElement))
        {
            return new WorkflowNodeDeprecationMetadata();
        }

        string deprecationPath = Append(path, "deprecation");
        RequireObject(deprecationElement, deprecationPath);
        RejectUnknownProperties(deprecationElement, deprecationPath, ["deprecated", "sinceVersion", "message", "replacementType", "replacementVersion"]);
        return new WorkflowNodeDeprecationMetadata(
            ReadOptionalBoolean(deprecationElement, "deprecated", Append(deprecationPath, "deprecated"), false),
            ReadOptionalString(deprecationElement, "sinceVersion", Append(deprecationPath, "sinceVersion")),
            ReadOptionalString(deprecationElement, "message", Append(deprecationPath, "message")),
            ReadOptionalString(deprecationElement, "replacementType", Append(deprecationPath, "replacementType")),
            deprecationElement.TryGetProperty("replacementVersion", out JsonElement replacementVersion)
                ? ReadRequiredIntValue(replacementVersion, Append(deprecationPath, "replacementVersion"))
                : null);
    }

    private static IReadOnlyList<JsonObject> ReadExamples(JsonElement element, string path)
    {
        if (!element.TryGetProperty("parameterExamples", out JsonElement examplesElement))
        {
            return [];
        }

        RequireArray(examplesElement, Append(path, "parameterExamples"));
        List<JsonObject> examples = [];
        int index = 0;
        foreach (JsonElement example in examplesElement.EnumerateArray())
        {
            if (JsonNode.Parse(example.GetRawText()) is not JsonObject exampleObject)
            {
                throw Create("Parameter examples must be objects.", Append(Append(path, "parameterExamples"), index));
            }

            examples.Add(exampleObject);
            index++;
        }

        return examples;
    }

    private static void WriteDocument(Utf8JsonWriter writer, NodeCatalogDocument document)
    {
        writer.WriteStartObject();
        writer.WriteString("$schema", document.Schema);
        writer.WriteString("specVersion", document.SpecVersion);
        writer.WriteString("id", document.Id);
        writer.WriteString("version", document.Version);
        WriteOptionalString(writer, "name", document.Name);
        WriteOptionalString(writer, "description", document.Description);
        writer.WritePropertyName("definitions");
        writer.WriteStartArray();
        foreach (WorkflowNodeDefinition definition in document.Definitions)
        {
            WriteDefinition(writer, definition);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string NormalizeJsonText(string json)
    {
        return json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n') + "\n";
    }

    private static void WriteDefinition(Utf8JsonWriter writer, WorkflowNodeDefinition definition)
    {
        writer.WriteStartObject();
        writer.WriteString("type", definition.Type);
        writer.WriteNumber("typeVersion", definition.Version);
        WriteOptionalString(writer, "displayName", definition.DisplayName);
        WriteOptionalString(writer, "description", definition.Description);
        WriteOptionalString(writer, "category", definition.Category);
        writer.WriteString("stability", WriteStability(definition.Stability));
        WriteStringArray(writer, "capabilities", definition.Capabilities);
        WriteBehavior(writer, definition.Behavior);
        WriteDeprecation(writer, definition.Deprecation);
        WriteOptionalJsonObject(writer, "parametersSchema", definition.ParametersSchema);
        WriteExamples(writer, definition.ParameterExamples);
        WritePorts(writer, "inputs", definition.Inputs);
        WritePorts(writer, "outputs", definition.Outputs);
        WriteDynamicPorts(writer, definition.DynamicPorts);
        WriteResources(writer, definition.Resources);
        WriteLocators(writer, definition.Locators);
        writer.WriteEndObject();
    }

    private static void WritePorts(Utf8JsonWriter writer, string propertyName, IReadOnlyDictionary<string, WorkflowPortDefinition> ports)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        foreach (KeyValuePair<string, WorkflowPortDefinition> port in ports)
        {
            writer.WritePropertyName(port.Key);
            writer.WriteStartObject();
            writer.WriteString("direction", WriteDirection(port.Value.Direction));
            writer.WriteBoolean("required", port.Value.Required);
            writer.WriteBoolean("allowsMultiple", port.Value.AllowsMultiple);
            WriteOptionalString(writer, "valueType", port.Value.ValueType);
            WriteOptionalJsonObject(writer, "schema", port.Value.Schema);
            WriteOptionalString(writer, "description", port.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteDynamicPorts(Utf8JsonWriter writer, IReadOnlyList<WorkflowDynamicPortRule> rules)
    {
        writer.WritePropertyName("dynamicPorts");
        writer.WriteStartArray();
        foreach (WorkflowDynamicPortRule rule in rules)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", WriteDynamicPortKind(rule.Kind));
            writer.WriteString("direction", WriteDirection(rule.Direction));
            writer.WriteString("sourcePointer", rule.SourcePointer);
            writer.WriteString("idPointer", rule.IdPointer);
            WriteOptionalString(writer, "description", rule.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteResources(Utf8JsonWriter writer, IReadOnlyDictionary<string, WorkflowNodeResourceRequirement> resources)
    {
        writer.WritePropertyName("resources");
        writer.WriteStartObject();
        foreach (KeyValuePair<string, WorkflowNodeResourceRequirement> resource in resources)
        {
            writer.WritePropertyName(resource.Key);
            writer.WriteStartObject();
            writer.WriteString("kind", resource.Value.Kind);
            writer.WriteBoolean("required", resource.Value.Required);
            WriteStringArray(writer, "capabilities", resource.Value.Capabilities);
            WriteOptionalString(writer, "description", resource.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteLocators(Utf8JsonWriter writer, IReadOnlyDictionary<string, NodeLocatorSlotDefinition> locators)
    {
        writer.WritePropertyName("locators");
        writer.WriteStartObject();
        foreach (KeyValuePair<string, NodeLocatorSlotDefinition> locator in locators)
        {
            writer.WritePropertyName(locator.Key);
            writer.WriteStartObject();
            writer.WriteString("parameterPointer", locator.Value.ParameterPointer);
            writer.WriteBoolean("required", locator.Value.Required);
            writer.WriteString("usage", WriteLocatorUsage(locator.Value.Usage));
            writer.WritePropertyName("acceptedCardinalities");
            writer.WriteStartArray();
            foreach (LocatorCardinality cardinality in locator.Value.AcceptedCardinalities)
            {
                writer.WriteStringValue(WriteLocatorCardinality(cardinality));
            }

            writer.WriteEndArray();
            WriteOptionalString(writer, "description", locator.Value.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteBehavior(Utf8JsonWriter writer, WorkflowNodeBehaviorMetadata behavior)
    {
        writer.WritePropertyName("behavior");
        writer.WriteStartObject();
        writer.WriteString("kind", WriteBehaviorKind(behavior.Kind));
        writer.WriteBoolean("terminal", behavior.Terminal);
        writer.WriteBoolean("maySuspend", behavior.MaySuspend);
        WriteOptionalString(writer, "description", behavior.Description);
        writer.WriteEndObject();
    }

    private static void WriteDeprecation(Utf8JsonWriter writer, WorkflowNodeDeprecationMetadata deprecation)
    {
        writer.WritePropertyName("deprecation");
        writer.WriteStartObject();
        writer.WriteBoolean("deprecated", deprecation.Deprecated);
        WriteOptionalString(writer, "sinceVersion", deprecation.SinceVersion);
        WriteOptionalString(writer, "message", deprecation.Message);
        WriteOptionalString(writer, "replacementType", deprecation.ReplacementType);
        if (deprecation.ReplacementVersion is not null)
        {
            writer.WriteNumber("replacementVersion", deprecation.ReplacementVersion.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteExamples(Utf8JsonWriter writer, IReadOnlyList<JsonObject> examples)
    {
        writer.WritePropertyName("parameterExamples");
        writer.WriteStartArray();
        foreach (JsonObject example in examples)
        {
            example.WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement arrayElement))
        {
            return [];
        }

        RequireArray(arrayElement, path);
        List<string> values = [];
        int index = 0;
        foreach (JsonElement value in arrayElement.EnumerateArray())
        {
            values.Add(ReadRequiredStringValue(value, Append(path, index)));
            index++;
        }

        return values;
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static JsonObject? ReadOptionalObject(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (JsonNode.Parse(property.GetRawText()) is not JsonObject jsonObject)
        {
            throw Create("Expected a JSON object.", path);
        }

        return jsonObject;
    }

    private static void WriteOptionalJsonObject(Utf8JsonWriter writer, string propertyName, JsonObject? value)
    {
        if (value is not null)
        {
            writer.WritePropertyName(propertyName);
            value.WriteTo(writer);
        }
    }

    private static WorkflowPortDirection ReadDirection(string value, string path)
    {
        return value switch
        {
            "input" => WorkflowPortDirection.Input,
            "output" => WorkflowPortDirection.Output,
            _ => throw Create($"Unknown port direction '{value}'.", path),
        };
    }

    private static string WriteDirection(WorkflowPortDirection value)
    {
        return value switch
        {
            WorkflowPortDirection.Input => "input",
            WorkflowPortDirection.Output => "output",
            _ => throw new InvalidOperationException($"Unknown port direction '{value}'."),
        };
    }

    private static WorkflowDynamicPortRuleKind ReadDynamicPortKind(string value, string path)
    {
        return value switch
        {
            "switch-cases" => WorkflowDynamicPortRuleKind.SwitchCases,
            _ => throw Create($"Unknown dynamic port rule kind '{value}'.", path),
        };
    }

    private static string WriteDynamicPortKind(WorkflowDynamicPortRuleKind value)
    {
        return value switch
        {
            WorkflowDynamicPortRuleKind.SwitchCases => "switch-cases",
            _ => throw new InvalidOperationException($"Unknown dynamic port rule kind '{value}'."),
        };
    }

    private static WorkflowNodeBehaviorKind ReadBehaviorKind(string value, string path)
    {
        return value switch
        {
            "action" => WorkflowNodeBehaviorKind.Action,
            "entry" => WorkflowNodeBehaviorKind.Entry,
            "terminal" => WorkflowNodeBehaviorKind.Terminal,
            "branch" => WorkflowNodeBehaviorKind.Branch,
            "loop" => WorkflowNodeBehaviorKind.Loop,
            "invocation" => WorkflowNodeBehaviorKind.Invocation,
            "interaction" => WorkflowNodeBehaviorKind.Interaction,
            _ => throw Create($"Unknown behavior kind '{value}'.", path),
        };
    }

    private static string WriteBehaviorKind(WorkflowNodeBehaviorKind value)
    {
        return value switch
        {
            WorkflowNodeBehaviorKind.Action => "action",
            WorkflowNodeBehaviorKind.Entry => "entry",
            WorkflowNodeBehaviorKind.Terminal => "terminal",
            WorkflowNodeBehaviorKind.Branch => "branch",
            WorkflowNodeBehaviorKind.Loop => "loop",
            WorkflowNodeBehaviorKind.Invocation => "invocation",
            WorkflowNodeBehaviorKind.Interaction => "interaction",
            _ => throw new InvalidOperationException($"Unknown behavior kind '{value}'."),
        };
    }

    private static WorkflowNodeStability ReadStability(string value, string path)
    {
        return value switch
        {
            "experimental" => WorkflowNodeStability.Experimental,
            "preview" => WorkflowNodeStability.Preview,
            "stable" => WorkflowNodeStability.Stable,
            _ => throw Create($"Unknown stability '{value}'.", path),
        };
    }

    private static string WriteStability(WorkflowNodeStability value)
    {
        return value switch
        {
            WorkflowNodeStability.Experimental => "experimental",
            WorkflowNodeStability.Preview => "preview",
            WorkflowNodeStability.Stable => "stable",
            _ => throw new InvalidOperationException($"Unknown stability '{value}'."),
        };
    }

    private static LocatorUsageMode ReadLocatorUsage(string value, string path)
    {
        return value switch
        {
            "single" => LocatorUsageMode.Single,
            "collection" => LocatorUsageMode.Collection,
            "optional-single" => LocatorUsageMode.OptionalSingle,
            _ => throw Create($"Unknown locator usage mode '{value}'.", path),
        };
    }

    private static string WriteLocatorUsage(LocatorUsageMode value)
    {
        return value switch
        {
            LocatorUsageMode.Single => "single",
            LocatorUsageMode.Collection => "collection",
            LocatorUsageMode.OptionalSingle => "optional-single",
            _ => throw new InvalidOperationException($"Unknown locator usage mode '{value}'."),
        };
    }

    private static IReadOnlyList<LocatorCardinality> ReadLocatorCardinalities(JsonElement element, string path)
    {
        if (!element.TryGetProperty("acceptedCardinalities", out JsonElement cardinalitiesElement))
        {
            return [];
        }

        RequireArray(cardinalitiesElement, Append(path, "acceptedCardinalities"));
        List<LocatorCardinality> cardinalities = [];
        int index = 0;
        foreach (JsonElement value in cardinalitiesElement.EnumerateArray())
        {
            cardinalities.Add(ReadLocatorCardinality(ReadRequiredStringValue(value, Append(Append(path, "acceptedCardinalities"), index)), Append(Append(path, "acceptedCardinalities"), index)));
            index++;
        }

        return cardinalities;
    }

    private static LocatorCardinality ReadLocatorCardinality(string value, string path)
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

    private static string WriteLocatorCardinality(LocatorCardinality value)
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

    private static int ReadRequiredInt(JsonElement element, string propertyName, string path)
    {
        return ReadRequiredIntValue(ReadRequiredProperty(element, propertyName, path), path);
    }

    private static string ReadRequiredStringValue(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.String)
        {
            throw Create("Expected a JSON string.", path);
        }

        return element.GetString() ?? throw Create("Required string value cannot be null.", path);
    }

    private static int ReadRequiredIntValue(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw Create("Expected a JSON integer.", path);
        }

        return value;
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
        return path + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static NodeCatalogSerializationException Create(string message, string path)
    {
        return new NodeCatalogSerializationException($"{message} Path '{path}'.");
    }
}
