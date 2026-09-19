using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>Executes deterministic SHA-256 hashing over canonical SkeletonKey JSON.</summary>
public sealed class DataHashHandler : INodeHandler
{
    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("data.hash", 1);

    /// <inheritdoc />
    public ValueTask<NodeHandlerResult> ExecuteAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Parameters.TryGetPropertyValue("value", out JsonNode? value))
        {
            return ValueTask.FromResult(Failure(request, "Parameter 'value' is required."));
        }

        string algorithm = request.Parameters["algorithm"] is JsonValue algorithmValue &&
            algorithmValue.GetValueKind() == JsonValueKind.String
            ? algorithmValue.GetValue<string>()
            : "sha256";
        if (!string.Equals(algorithm, "sha256", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(Failure(request, "Only sha256 is supported."));
        }

        byte[] canonical = CanonicalJson(value);
        string hash = Convert.ToHexStringLower(SHA256.HashData(canonical));
        Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
        {
            ["hash"] = new([JsonValue.Create(hash)]),
            ["algorithm"] = new([JsonValue.Create("sha256")]),
        };
        return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], outputs)));
    }

    private static NodeHandlerResult Failure(NodeExecutionRequest request, string message)
    {
        return NodeHandlerResult.Failure(new WorkflowError("SKD1001", message, request.Identity.NodeId));
    }

    private static byte[] CanonicalJson(JsonNode? value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, value);
            writer.Flush();
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Key);
                WriteCanonical(writer, property.Value);
            }

            writer.WriteEndObject();
            return;
        }

        if (value is JsonArray array)
        {
            writer.WriteStartArray();
            foreach (JsonNode? item in array)
            {
                WriteCanonical(writer, item);
            }

            writer.WriteEndArray();
            return;
        }

        value.WriteTo(writer);
    }
}
