using System.Text.Json.Nodes;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Http.Abstractions;
using SkeletonKey.Http.BuiltIns;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Confirms the Phase 1 provider-neutral HTTP capability.</summary>
public sealed class Phase1HttpCapabilityTests
{
    /// <summary>Verifies the handler serializes JSON and exposes structured response outputs.</summary>
    [Fact]
    public async Task HttpRequestHandlerPostsJsonThroughProviderNeutralTransport()
    {
        RecordingTransport transport = new();
        HttpRequestHandler handler = new(transport);
        NodeExecutionIdentity identity = Identity("http.request");
        NodeExecutionRequest request = new(
            identity,
            new JsonObject
            {
                ["method"] = "POST",
                ["url"] = "https://example.test/respond",
                ["headers"] = new JsonObject { ["X-Test"] = "phase1" },
                ["json"] = new JsonObject { ["message"] = "hello" },
            });

        NodeHandlerResult result = await handler.ExecuteAsync(request, new StubExecutionContext(identity));

        Assert.Equal(NodeHandlerCompletionStatus.Succeeded, result.Status);
        Assert.NotNull(transport.LastRequest);
        Assert.Equal("POST", transport.LastRequest!.Method);
        Assert.Equal("application/json", transport.LastRequest.ContentType);
        Assert.Equal("{\"message\":\"hello\"}", transport.LastRequest.Body);
        Assert.Equal("phase1", transport.LastRequest.Headers["X-Test"]);
        Assert.Equal(200, result.Outputs.DataOutputs["status"].Values[0]!.GetValue<int>());
        Assert.Equal("ok", result.Outputs.DataOutputs["json"].Values[0]!["answer"]!.GetValue<string>());
    }

    /// <summary>Verifies the HTTP catalog reserves exactly the generic request node.</summary>
    [Fact]
    public void HttpCatalogContainsRequestNode()
    {
        WorkflowNodeDefinition definition = Assert.Single(HttpBuiltInWorkflowNodeCatalog.Catalog.Definitions);
        Assert.Equal("http.request", definition.Type);
        Assert.Equal(1, definition.Version);
    }

    private static NodeExecutionIdentity Identity(string type)
    {
        return new NodeExecutionIdentity("execution", "invocation", null, "workflow", "node", new WorkflowNodeDefinitionKey(type, 1), "plan", "step", 1);
    }

    private sealed class RecordingTransport : IHttpTransport
    {
        public HttpTransportRequest? LastRequest { get; private set; }

        public ValueTask<HttpTransportResponse> SendAsync(HttpTransportRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            IReadOnlyDictionary<string, IReadOnlyList<string>> headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = Array.AsReadOnly(["application/json"]),
            };
            return ValueTask.FromResult(new HttpTransportResponse(200, headers, "{\"answer\":\"ok\"}", request.Uri));
        }
    }

    private sealed class StubExecutionContext(NodeExecutionIdentity identity) : INodeExecutionContext
    {
        public NodeExecutionIdentity Identity { get; } = identity;

        public INodeExecutionEventWriter Events => null!;

        public INodeResourceAccessor Resources => null!;

        public SkeletonKey.Locators.INodeLocatorAccessor Locators => null!;
    }
}
