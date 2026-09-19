using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers bounded automatic node failure diagnostics.</summary>
public sealed class Phase5DiagnosticsTests
{
    /// <summary>Verifies diagnostics are redacted and a contributor failure never replaces the original node failure.</summary>
    [Fact]
    public async Task FailureDiagnosticsAreRedactedAndPreserveOriginalError()
    {
        const string secret = "phase5-sensitive-token";
        WorkflowRuntimeResult result = await Runtime(
            new WorkflowRuntimeOptions(),
            [new SensitiveContributor(secret), new ThrowingContributor()]).ExecuteAsync(
            new WorkflowExecutionRequest(Workflow(), "phase5-diagnostics", "phase5-diagnostics-plan"));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        WorkflowError error = Assert.IsType<WorkflowError>(result.Result.Error);
        Assert.Equal("DEMO_FAILURE", error.Code);
        Assert.Equal("original failure", error.Message);
        JsonObject details = Assert.IsType<JsonObject>(error.Details);
        string json = details.ToJsonString();
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.Contains("safe-value", json, StringComparison.Ordinal);
        Assert.Contains("\"captureFailed\":true", json, StringComparison.Ordinal);
    }

    /// <summary>Verifies host policy can disable diagnostic capture completely.</summary>
    [Fact]
    public async Task FailureDiagnosticsCanBeDisabled()
    {
        WorkflowRuntimeResult result = await Runtime(
            new WorkflowRuntimeOptions(enableFailureDiagnostics: false),
            [new SensitiveContributor("secret")]).ExecuteAsync(
            new WorkflowExecutionRequest(Workflow(), "phase5-diagnostics-off", "phase5-diagnostics-plan"));

        WorkflowError error = Assert.IsType<WorkflowError>(result.Result.Error);
        Assert.Null(error.Details);
    }

    private static DefaultWorkflowRuntime Runtime(
        WorkflowRuntimeOptions options,
        IReadOnlyList<INodeFailureDiagnosticContributor> contributors)
    {
        WorkflowNodeDefinition failure = new(
            "demo.fail",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Ports(WorkflowPortDirection.Output, "next"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, failure]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), new FailureHandler()]),
            new NodeParameterMaterializer(),
            options: options,
            diagnosticContributors: contributors);
    }

    private static WorkflowDocument Workflow()
    {
        return new WorkflowDocument(
            id: "phase5-diagnostic-workflow",
            name: "Phase 5 Diagnostic Workflow",
            nodes:
            [
                new("start", "core.start", 1),
                new("failure", "demo.fail", 1),
                new("done", "core.return", 1, parameters: new JsonObject
                {
                    ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" },
                }),
            ],
            connections:
            [
                Connect("start", "main", "failure", "main"),
                Connect("failure", "next", "done", "main"),
            ]);
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction),
        };
    }

    private sealed class FailureHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.fail", 1);

        public ValueTask<NodeHandlerResult> ExecuteAsync(
            NodeExecutionRequest request,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Failure(
                new WorkflowError("DEMO_FAILURE", "original failure", request.Identity.NodeId)));
        }
    }

    private sealed class SensitiveContributor(string secret) : INodeFailureDiagnosticContributor
    {
        public string Id => "test.sensitive";

        public bool AppliesTo(WorkflowNodeDefinitionKey definition) => definition.Type == "demo.fail";

        public ValueTask<NodeFailureDiagnosticContribution?> CaptureAsync(
            NodeExecutionIdentity identity,
            WorkflowError error,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NodeFailureDiagnosticContribution contribution = new(Id, new JsonObject
            {
                ["authorization"] = secret,
                ["nested"] = new JsonObject
                {
                    ["apiToken"] = secret,
                    ["message"] = "safe-value",
                },
            });
            return ValueTask.FromResult<NodeFailureDiagnosticContribution?>(contribution);
        }
    }

    private sealed class ThrowingContributor : INodeFailureDiagnosticContributor
    {
        public string Id => "test.throwing";

        public bool AppliesTo(WorkflowNodeDefinitionKey definition) => definition.Type == "demo.fail";

        public ValueTask<NodeFailureDiagnosticContribution?> CaptureAsync(
            NodeExecutionIdentity identity,
            WorkflowError error,
            INodeExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("diagnostic capture failed");
        }
    }
}
