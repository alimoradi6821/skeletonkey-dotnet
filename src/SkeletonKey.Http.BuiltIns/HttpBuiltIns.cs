using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Http.Abstractions;

namespace SkeletonKey.Http.BuiltIns;

/// <summary>Provides the built-in HTTP node catalog.</summary>
public static class HttpBuiltInWorkflowNodeCatalog
{
    /// <summary>Gets the HTTP catalog document.</summary>
    public static NodeCatalogDocument Document { get; } = new(
        id: "skeletonkey-http-builtins",
        version: "0.1.0",
        name: "SkeletonKey HTTP Built-in Nodes",
        definitions: [RequestDefinition()]);

    /// <summary>Gets the immutable HTTP catalog.</summary>
    public static WorkflowNodeDefinitionCatalog Catalog { get; } = new(Document.Definitions);

    private static WorkflowNodeDefinition RequestDefinition()
    {
        return new WorkflowNodeDefinition(
            "http.request",
            1,
            displayName: "HTTP Request",
            category: "http",
            parametersSchema: new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray(JsonValue.Create("url")),
            },
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["main"] = new("main", WorkflowPortDirection.Input),
            },
            outputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["continue"] = new("continue", WorkflowPortDirection.Output),
                ["status"] = new("status", WorkflowPortDirection.Output, roles: ["data"]),
                ["text"] = new("text", WorkflowPortDirection.Output, roles: ["data"]),
                ["json"] = new("json", WorkflowPortDirection.Output, roles: ["data"]),
                ["headers"] = new("headers", WorkflowPortDirection.Output, roles: ["data"]),
                ["finalUrl"] = new("finalUrl", WorkflowPortDirection.Output, roles: ["data"]),
            },
            behavior: new WorkflowNodeBehaviorMetadata(WorkflowNodeBehaviorKind.Action),
            stability: WorkflowNodeStability.Preview,
            parameterExamples:
            [
                new JsonObject
                {
                    ["method"] = "POST",
                    ["url"] = "https://example.test/api",
                    ["json"] = new JsonObject { ["message"] = "hello" },
                    ["timeoutMilliseconds"] = 30000,
                    ["maximumResponseBytes"] = 1048576,
                },
            ]);
    }
}

/// <summary>Creates HTTP built-in handlers over an explicit provider-neutral transport.</summary>
public static class HttpBuiltInRuntimeHandlers
{
    /// <summary>Creates the HTTP handler set.</summary>
    public static IReadOnlyList<INodeHandler> Create(IHttpTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        return Array.AsReadOnly<INodeHandler>([new HttpRequestHandler(transport)]);
    }
}

/// <summary>Executes <c>http.request</c>.</summary>
public sealed class HttpRequestHandler(IHttpTransport transport) : IExternalSideEffectNodeHandler
{
    private readonly IHttpTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));

    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("http.request", 1);

    /// <inheritdoc />
    public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            JsonObject parameters = request.Parameters;
            string url = RequiredString(parameters, "url");
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return Failure(request, HttpCapabilityErrorCodes.InvalidRequest, "Parameter 'url' must be an absolute URI.");
            }

            string method = OptionalString(parameters, "method", "GET");
            Dictionary<string, string> headers = ParseHeaders(parameters["headers"]);
            bool hasText = parameters["body"] is not null;
            bool hasJson = parameters["json"] is not null;
            if (hasText && hasJson)
            {
                return Failure(request, HttpCapabilityErrorCodes.InvalidRequest, "Parameters 'body' and 'json' are mutually exclusive.");
            }

            string? body = hasText ? RequiredString(parameters, "body") : parameters["json"]?.ToJsonString();
            string? contentType = hasJson ? "application/json" : OptionalNullableString(parameters, "contentType");
            HttpTransportRequest transportRequest = new(
                method,
                uri,
                headers,
                body,
                contentType,
                OptionalInt(parameters, "timeoutMilliseconds", 30000),
                OptionalInt(parameters, "maximumResponseBytes", 1024 * 1024),
                OptionalBool(parameters, "followRedirects", true));

            HttpTransportResponse response = await _transport.SendAsync(transportRequest, cancellationToken).ConfigureAwait(false);
            JsonNode? parsedJson = null;
            if (OptionalBool(parameters, "parseJson", true) && !string.IsNullOrWhiteSpace(response.Body))
            {
                try
                {
                    parsedJson = JsonNode.Parse(response.Body);
                }
                catch (JsonException)
                {
                    parsedJson = null;
                }
            }

            JsonObject responseHeaders = new();
            foreach (KeyValuePair<string, IReadOnlyList<string>> header in response.Headers.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                responseHeaders[header.Key] = new JsonArray([.. header.Value.Select(static value => JsonValue.Create(value))]);
            }

            Dictionary<string, NodePortValueSet> outputs = new(StringComparer.Ordinal)
            {
                ["status"] = new([JsonValue.Create(response.StatusCode)]),
                ["text"] = new([JsonValue.Create(response.Body)]),
                ["json"] = new([parsedJson]),
                ["headers"] = new([responseHeaders]),
                ["finalUrl"] = new([JsonValue.Create(response.FinalUri.AbsoluteUri)]),
            };
            return NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], outputs));
        }
        catch (HttpCapabilityException exception)
        {
            return Failure(request, exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Failure(request, HttpCapabilityErrorCodes.InvalidRequest, exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NodeHandlerResult.Cancelled(new WorkflowError(HttpCapabilityErrorCodes.RequestCancelled, "HTTP request was cancelled.", request.Identity.NodeId));
        }
    }

    private static NodeHandlerResult Failure(NodeExecutionRequest request, string code, string message)
    {
        return NodeHandlerResult.Failure(new WorkflowError(code, message, request.Identity.NodeId));
    }

    private static string RequiredString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new HttpCapabilityException(HttpCapabilityErrorCodes.InvalidRequest, $"Parameter '{name}' must be a string.");
    }

    private static string OptionalString(JsonObject parameters, string name, string fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : fallback;
    }

    private static string? OptionalNullableString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
    }

    private static int OptionalInt(JsonObject parameters, string name, int fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<int>() : fallback;
    }

    private static bool OptionalBool(JsonObject parameters, string name, bool fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? value.GetValue<bool>() : fallback;
    }

    private static Dictionary<string, string> ParseHeaders(JsonNode? node)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (node is null)
        {
            return result;
        }

        if (node is not JsonObject headers)
        {
            throw new HttpCapabilityException(HttpCapabilityErrorCodes.InvalidRequest, "Parameter 'headers' must be an object of string values.");
        }

        foreach (KeyValuePair<string, JsonNode?> header in headers)
        {
            if (header.Value is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
            {
                throw new HttpCapabilityException(HttpCapabilityErrorCodes.InvalidRequest, "HTTP header values must be strings.");
            }

            result.Add(header.Key, value.GetValue<string>());
        }

        return result;
    }
}
