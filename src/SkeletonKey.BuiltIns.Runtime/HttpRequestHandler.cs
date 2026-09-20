using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>Executes a bounded provider-neutral HTTP request for workflow integrations.</summary>
public sealed class HttpRequestHandler : INodeHandler
{
    private const int MaximumResponseBytes = 4 * 1024 * 1024;
    private static readonly HttpClient _client = new(new SocketsHttpHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
    })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

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
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            {
                return Failure(request, "SKB1200", "http.request requires an absolute HTTP or HTTPS URL.");
            }

            string methodText = OptionalString(parameters, "method", "GET").ToUpperInvariant();
            if (methodText is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"))
            {
                return Failure(request, "SKB1201", "http.request method is not supported.");
            }

            int timeoutMilliseconds = OptionalInt(parameters, "timeoutMilliseconds", 60000);
            if (timeoutMilliseconds is <= 0 or > 300000)
            {
                return Failure(request, "SKB1202", "http.request timeoutMilliseconds must be between 1 and 300000.");
            }

            using HttpRequestMessage message = new(new HttpMethod(methodText), uri);
            if (parameters["headers"] is JsonObject headers)
            {
                foreach (KeyValuePair<string, JsonNode?> header in headers)
                {
                    if (header.Value is not JsonValue headerValue || headerValue.GetValueKind() != JsonValueKind.String)
                    {
                        return Failure(request, "SKB1203", "http.request header values must be strings.");
                    }

                    string headerText = headerValue.GetValue<string>();
                    if (!message.Headers.TryAddWithoutValidation(header.Key, headerText))
                    {
                        return Failure(request, "SKB1204", "http.request contains an unsupported request header.");
                    }
                }
            }

            string bearerToken = OptionalString(parameters, "bearerToken", string.Empty);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (parameters.TryGetPropertyValue("jsonBody", out JsonNode? jsonBody) && jsonBody is not null)
            {
                message.Content = new StringContent(jsonBody.ToJsonString(), Encoding.UTF8, "application/json");
            }
            else if (parameters["body"] is JsonValue bodyValue && bodyValue.GetValueKind() == JsonValueKind.String)
            {
                string contentType = OptionalString(parameters, "contentType", "text/plain");
                message.Content = new StringContent(bodyValue.GetValue<string>(), Encoding.UTF8, contentType);
            }

            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMilliseconds);
            using HttpResponseMessage response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            string body = await ReadBoundedBodyAsync(response, timeout.Token).ConfigureAwait(false);

            bool ensureSuccess = OptionalBool(parameters, "ensureSuccess", true);
            if (ensureSuccess && !response.IsSuccessStatusCode)
            {
                int status = (int)response.StatusCode;
                return NodeHandlerResult.Failure(new WorkflowError(
                    "SKB1205",
                    "HTTP request returned a non-success status code: " + status.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
                    request.Identity.NodeId,
                    retryable: status == 429 || status >= 500));
            }

            JsonNode? json = TryParseJson(body);
            JsonNode? selected = SelectResponse(json, body, parameters["responseJsonPointers"] as JsonArray);

            Dictionary<string, NodePortValueSet> data = new(StringComparer.Ordinal)
            {
                ["status"] = new([JsonValue.Create((int)response.StatusCode)]),
                ["body"] = new([JsonValue.Create(body)]),
                ["json"] = new([json?.DeepClone()]),
                ["selected"] = new([selected?.DeepClone()]),
            };
            return NodeHandlerResult.Success(new NodeHandlerOutputs(["continue"], data));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return NodeHandlerResult.Cancelled(new WorkflowError("SKB1206", "HTTP request was cancelled.", request.Identity.NodeId));
        }
        catch (OperationCanceledException)
        {
            return Failure(request, "SKB1207", "HTTP request timed out.", retryable: true);
        }
        catch (HttpRequestException)
        {
            return Failure(request, "SKB1208", "HTTP request failed.", retryable: true);
        }
        catch (IOException)
        {
            return Failure(request, "SKB1209", "HTTP response could not be read.", retryable: true);
        }
        catch (JsonException)
        {
            return Failure(request, "SKB1210", "HTTP request parameters contain invalid JSON.");
        }
    }

    private static async Task<string> ReadBoundedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] block = new byte[8192];
        while (true)
        {
            int read = await stream.ReadAsync(block, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new IOException("HTTP response exceeds the maximum allowed size.");
            }

            buffer.Write(block, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static JsonNode? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonNode? SelectResponse(JsonNode? json, string body, JsonArray? pointers)
    {
        if (json is not null && pointers is not null)
        {
            foreach (JsonNode? pointerNode in pointers)
            {
                if (pointerNode is JsonValue pointerValue &&
                    pointerValue.GetValueKind() == JsonValueKind.String &&
                    TryResolvePointer(json, pointerValue.GetValue<string>(), out JsonNode? selected) &&
                    selected is not null)
                {
                    return selected.DeepClone();
                }
            }
        }

        if (json is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            return JsonValue.Create(value.GetValue<string>());
        }

        return JsonValue.Create(body);
    }

    private static bool TryResolvePointer(JsonNode root, string pointer, out JsonNode? value)
    {
        value = root;
        if (pointer.Length == 0)
        {
            return true;
        }

        if (!pointer.StartsWith('/', StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        foreach (string encoded in pointer.Split('/').Skip(1))
        {
            string token = encoded.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (value is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(token, out value))
                {
                    return false;
                }
            }
            else if (value is JsonArray array &&
                     int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index) &&
                     index >= 0 && index < array.Count)
            {
                value = array[index];
            }
            else
            {
                value = null;
                return false;
            }
        }

        return true;
    }

    private static NodeHandlerResult Failure(NodeExecutionRequest request, string code, string message, bool retryable = false)
    {
        return NodeHandlerResult.Failure(new WorkflowError(code, message, request.Identity.NodeId, retryable));
    }

    private static string RequiredString(JsonObject parameters, string name)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : throw new JsonException("Required string parameter is missing.");
    }

    private static string OptionalString(JsonObject parameters, string name, string fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : fallback;
    }

    private static int OptionalInt(JsonObject parameters, string name, int fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number
            ? value.GetValue<int>()
            : fallback;
    }

    private static bool OptionalBool(JsonObject parameters, string name, bool fallback)
    {
        return parameters[name] is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            ? value.GetValue<bool>()
            : fallback;
    }
}
