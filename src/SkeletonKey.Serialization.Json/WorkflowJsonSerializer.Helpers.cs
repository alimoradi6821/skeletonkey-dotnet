using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Serialization.Json.Internal;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Policies;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Serialization.Json;

public sealed partial class WorkflowJsonSerializer
{
    private static void RejectUnknownProperties(JsonElement element, string path, string[] knownProperties)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!knownProperties.Contains(property.Name))
            {
                throw JsonExceptionFactory.Create($"Unknown property '{property.Name}' is not allowed.", Append(path, property.Name));
            }
        }
    }

    private static JsonElement ReadRequiredProperty(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            throw JsonExceptionFactory.Create($"Required property '{propertyName}' is missing.", path);
        }

        if (property.ValueKind is JsonValueKind.Null)
        {
            throw JsonExceptionFactory.Create($"Required property '{propertyName}' cannot be null.", path);
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
            throw JsonExceptionFactory.Create("Expected a JSON string.", path);
        }

        return element.GetString() ?? throw JsonExceptionFactory.Create("Required string value cannot be null.", path);
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return ReadRequiredStringValue(property, path);
    }

    private static string? ReadOptionalNonNullString(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return ReadRequiredStringValue(property, path);
    }

    private static int ReadRequiredInt32(JsonElement element, string propertyName, string path)
    {
        return ReadInt32Value(ReadRequiredProperty(element, propertyName, path), path);
    }

    private static double ReadRequiredDouble(JsonElement element, string propertyName, string path)
    {
        return ReadDoubleValue(ReadRequiredProperty(element, propertyName, path), path);
    }

    private static int ReadInt32Value(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw JsonExceptionFactory.Create("Expected a JSON integer.", path);
        }

        return value;
    }

    private static double ReadDoubleValue(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Number || !element.TryGetDouble(out double value))
        {
            throw JsonExceptionFactory.Create("Expected a JSON number.", path);
        }

        return value;
    }

    private static bool ReadOptionalBoolean(JsonElement element, string propertyName, string path, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return defaultValue;
        }

        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw JsonExceptionFactory.Create("Expected a JSON boolean.", path);
        }

        return property.GetBoolean();
    }

    private static void RequireObject(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw JsonExceptionFactory.Create("Expected a JSON object.", path);
        }
    }

    private static void RequireArray(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Array)
        {
            throw JsonExceptionFactory.Create("Expected a JSON array.", path);
        }
    }

    private static JsonNode? ToJsonNode(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return JsonNode.Parse(element.GetRawText());
    }

    private static JsonObject? ReadOptionalJsonObject(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        RequireObject(property, path);
        return (JsonObject)(ToJsonNode(property) ?? new JsonObject());
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteJsonNode(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is null)
        {
            writer.WriteNullValue();
            return;
        }

        node.WriteTo(writer);
    }

    private static WorkflowInputType ReadInputType(string value, string path)
    {
        return value switch
        {
            "string" => WorkflowInputType.String,
            "integer" => WorkflowInputType.Integer,
            "number" => WorkflowInputType.Number,
            "boolean" => WorkflowInputType.Boolean,
            "object" => WorkflowInputType.Object,
            "array" => WorkflowInputType.Array,
            _ => throw JsonExceptionFactory.Create($"Unknown workflow input type '{value}'.", path),
        };
    }

    private static string WriteInputType(WorkflowInputType value)
    {
        return value switch
        {
            WorkflowInputType.String => "string",
            WorkflowInputType.Integer => "integer",
            WorkflowInputType.Number => "number",
            WorkflowInputType.Boolean => "boolean",
            WorkflowInputType.Object => "object",
            WorkflowInputType.Array => "array",
            _ => throw new InvalidOperationException($"Unknown workflow input type '{value}'."),
        };
    }

    private static WorkflowOutputMode ReadOutputMode(string value, string path)
    {
        return value switch
        {
            "single" => WorkflowOutputMode.Single,
            "collection" => WorkflowOutputMode.Collection,
            "stream" => WorkflowOutputMode.Stream,
            _ => throw JsonExceptionFactory.Create($"Unknown workflow output mode '{value}'.", path),
        };
    }

    private static string WriteOutputMode(WorkflowOutputMode value)
    {
        return value switch
        {
            WorkflowOutputMode.Single => "single",
            WorkflowOutputMode.Collection => "collection",
            WorkflowOutputMode.Stream => "stream",
            _ => throw new InvalidOperationException($"Unknown workflow output mode '{value}'."),
        };
    }

    private static WorkflowResourceLifetime ReadResourceLifetime(string value, string path)
    {
        return value switch
        {
            "execution" => WorkflowResourceLifetime.Execution,
            "invocation" => WorkflowResourceLifetime.Invocation,
            _ => throw JsonExceptionFactory.Create($"Unknown workflow resource lifetime '{value}'.", path),
        };
    }

    private static string WriteResourceLifetime(WorkflowResourceLifetime value)
    {
        return value switch
        {
            WorkflowResourceLifetime.Execution => "execution",
            WorkflowResourceLifetime.Invocation => "invocation",
            _ => throw new InvalidOperationException($"Unknown workflow resource lifetime '{value}'."),
        };
    }

    private static WorkflowResourceAccessMode ReadResourceAccess(string value, string path)
    {
        return value switch
        {
            "exclusive" => WorkflowResourceAccessMode.Exclusive,
            "shared" => WorkflowResourceAccessMode.Shared,
            _ => throw JsonExceptionFactory.Create($"Unknown workflow resource access '{value}'.", path),
        };
    }

    private static string WriteResourceAccess(WorkflowResourceAccessMode value)
    {
        return value switch
        {
            WorkflowResourceAccessMode.Exclusive => "exclusive",
            WorkflowResourceAccessMode.Shared => "shared",
            _ => throw new InvalidOperationException($"Unknown workflow resource access '{value}'."),
        };
    }

    private static WorkflowOnError ReadOnError(string value, string path)
    {
        return value switch
        {
            "fail" => WorkflowOnError.Fail,
            "continue" => WorkflowOnError.Continue,
            "stop" => WorkflowOnError.Stop,
            _ => throw JsonExceptionFactory.Create($"Unknown onError value '{value}'.", path),
        };
    }

    private static string WriteOnError(WorkflowOnError value)
    {
        return value switch
        {
            WorkflowOnError.Fail => "fail",
            WorkflowOnError.Continue => "continue",
            WorkflowOnError.Stop => "stop",
            _ => throw new InvalidOperationException($"Unknown onError value '{value}'."),
        };
    }

    private static void ValidatePath(string path, WorkflowSerializationOperation operation)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new WorkflowSerializationException(
                operation,
                "Workflow file path must not be null, empty, or whitespace.");
        }
    }

    private static WorkflowSerializationException CreateJsonException(
        WorkflowSerializationOperation operation,
        JsonException exception)
    {
        string? pointerPath = ToJsonPointer(exception.Path);
        return new WorkflowSerializationException(
            operation,
            FormatDeserializeMessage(pointerPath, exception.LineNumber, exception.BytePositionInLine),
            pointerPath,
            exception.LineNumber,
            exception.BytePositionInLine,
            exception);
    }

    private static string FormatDeserializeMessage(string? pointerPath, long? lineNumber, long? bytePositionInLine)
    {
        StringBuilder builder = new("Failed to deserialize workflow JSON");

        if (!string.IsNullOrEmpty(pointerPath))
        {
            builder.Append(CultureInfo.InvariantCulture, $" at path '{pointerPath}'");
        }

        if (lineNumber.HasValue)
        {
            builder.Append(CultureInfo.InvariantCulture, $", line {lineNumber.Value}");
        }

        if (bytePositionInLine.HasValue)
        {
            builder.Append(CultureInfo.InvariantCulture, $", byte {bytePositionInLine.Value}");
        }

        builder.Append('.');
        return builder.ToString();
    }

    private static string? ToJsonPointer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith('/'))
        {
            return path;
        }

        if (path == "$")
        {
            return string.Empty;
        }

        if (!path.StartsWith('$'))
        {
            return path;
        }

        List<string> segments = [];
        int index = 1;
        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                index++;
                int start = index;
                while (index < path.Length && path[index] != '.' && path[index] != '[')
                {
                    index++;
                }

                segments.Add(EscapeJsonPointerToken(path[start..index]));
            }
            else if (path[index] == '[')
            {
                index++;
                int start = index;
                while (index < path.Length && path[index] != ']')
                {
                    index++;
                }

                segments.Add(EscapeJsonPointerToken(path[start..index]));
                if (index < path.Length && path[index] == ']')
                {
                    index++;
                }
            }
            else
            {
                index++;
            }
        }

        return segments.Count == 0 ? string.Empty : "/" + string.Join('/', segments);
    }

    private static string Append(string path, string token)
    {
        return path + "/" + EscapeJsonPointerToken(token);
    }

    private static string Append(string path, int index)
    {
        return path + "/" + index.ToString(CultureInfo.InvariantCulture);
    }

    private static string EscapeJsonPointerToken(string token)
    {
        return token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

