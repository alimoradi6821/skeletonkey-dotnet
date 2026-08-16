using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Materialization;
using SkeletonKey.Planning;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
using SkeletonKey.Runtime.Interactions;
using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>
/// Covers default workflow runtime failure handling, event sequencing, scheduling, and cancellation behavior.
/// </summary>
public sealed class DefaultWorkflowRuntimeTests
{
    /// <summary>
    /// Verifies semantically invalid workflows are rejected before analysis and execution.
    /// </summary>
    [Fact]
    public async Task RejectsSemanticallyInvalidWorkflow()
    {
        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(new WorkflowDocument(id: "bad", name: "Bad", nodes: [])));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.SemanticValidationFailed, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies catalog-invalid workflows are rejected after semantic validation.
    /// </summary>
    [Fact]
    public async Task RejectsCatalogInvalidWorkflow()
    {
        WorkflowDocument workflow = Workflow([Start(), new("custom", "demo.missing", 1)], [Connect("start", "main", "custom", "main")]);

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.CatalogAnalysisFailed, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies planning failures are normalized before runtime state execution begins.
    /// </summary>
    [Fact]
    public async Task RejectsPlanningInvalidWorkflow()
    {
        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new BlockingPlanner(),
            BuiltInWorkflowNodeCatalog.Catalog,
            BuiltInRuntimeHandlers.CreateResolver());

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(Request(Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")])));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.PlanningFailed, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies successful execution creates runtime state and follows Created to Ready to Running to Completed.
    /// </summary>
    [Fact]
    public async Task CreatesExecutionStateAndTransitionsToCompleted()
    {
        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")])));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal(ExecutionLifecycleState.Completed, result.ExecutionSnapshot!.State);
        Assert.Equal(4, result.ExecutionSnapshot.Revision);
        Assert.Equal(2, result.NodeResults.Count);
    }

    /// <summary>
    /// Verifies a missing exact handler is a structured runtime failure.
    /// </summary>
    [Fact]
    public async Task RejectsMissingHandler()
    {
        WorkflowNodeDefinition node = ControlNode("demo.no-handler");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.no-handler", 1), Return("done")], [Connect("start", "main", "demo", "main"), Connect("demo", "next", "done", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(node), BuiltInRuntimeHandlers.Create()).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.MissingNodeHandler, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies handler identity mismatch is rejected as a runtime contract violation.
    /// </summary>
    [Fact]
    public async Task RejectsHandlerIdentityMismatch()
    {
        WorkflowNodeDefinition node = ControlNode("demo.mismatch");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.mismatch", 1), Return("done")], [Connect("start", "main", "demo", "main"), Connect("demo", "next", "done", "main")]);
        INodeHandlerResolver resolver = new MismatchResolver(new WrongIdentityHandler());

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new CoreReturnHandler()], resolver).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.HandlerIdentityMismatch, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies invalid handler control outputs fail the execution.
    /// </summary>
    [Fact]
    public async Task RejectsInvalidControlOutput()
    {
        WorkflowNodeDefinition node = ControlNode("demo.bad-control");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.bad-control", 1), Return("done")], [Connect("start", "main", "demo", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new BadControlHandler("demo.bad-control")]).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.InvalidHandlerControlOutput, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies invalid handler data outputs fail the execution.
    /// </summary>
    [Fact]
    public async Task RejectsInvalidDataOutput()
    {
        WorkflowNodeDefinition node = ControlNode("demo.bad-data");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.bad-data", 1)], [Connect("start", "main", "demo", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new BadDataHandler("demo.bad-data")]).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.InvalidHandlerDataOutput, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies expected handler failures are normalized without exception leakage.
    /// </summary>
    [Fact]
    public async Task NormalizesExpectedHandlerFailure()
    {
        WorkflowNodeDefinition node = ControlNode("demo.expected-failure");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.expected-failure", 1)], [Connect("start", "main", "demo", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new ExpectedFailureHandler("demo.expected-failure")]).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal("TEST1001", result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies unexpected handler exceptions are normalized into SKR1007.
    /// </summary>
    [Fact]
    public async Task NormalizesUnexpectedHandlerException()
    {
        WorkflowNodeDefinition node = ControlNode("demo.throw");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.throw", 1)], [Connect("start", "main", "demo", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new ThrowingHandler("demo.throw")]).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.HandlerUnexpectedException, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies cancellation before start returns a cancelled technical status.
    /// </summary>
    [Fact]
    public async Task CancelsBeforeStart()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")])), cancellation.Token);

        Assert.Equal(WorkflowExecutionStatus.Cancelled, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.ExecutionCancelled, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies cancellation during handler execution is classified as cancellation, not ordinary failure.
    /// </summary>
    [Fact]
    public async Task CancelsDuringHandlerExecution()
    {
        WorkflowNodeDefinition node = ControlNode("demo.cancel");
        WorkflowDocument workflow = Workflow([Start(), new("demo", "demo.cancel", 1)], [Connect("start", "main", "demo", "main")]);
        DefaultWorkflowRuntime runtime = Runtime(Catalog(node), [new CoreStartHandler(), new CancellingHandler("demo.cancel")]);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(Request(workflow), cancellation.Token);

        Assert.Equal(WorkflowExecutionStatus.Cancelled, result.Result.Status);
    }

    /// <summary>
    /// Verifies emitted runtime events are one-based and monotonic.
    /// </summary>
    [Fact]
    public async Task EmitsMonotonicEventSequence()
    {
        RecordingSink sink = new();

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")]), sink));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        RuntimeWorkflowEvent[] events = sink.Events.OfType<RuntimeWorkflowEvent>().ToArray();
        Assert.Equal(1, events[0].Sequence);
        Assert.Equal(events.Select(static item => item.Sequence), events.OrderBy(static item => item.Sequence).Select(static item => item.Sequence));
        Assert.Contains(events, static item => item.Kind == RuntimeWorkflowEventKind.NodeCompleted);
    }

    /// <summary>
    /// Verifies scheduler no-progress states fail deterministically.
    /// </summary>
    [Fact]
    public async Task DetectsNoProgressState()
    {
        WorkflowDocument workflow = Workflow([Start(), Return("done")], []);

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Failed, result.Result.Status);
        Assert.Equal(WorkflowRuntimeErrorCodes.ExecutionNoProgress, result.Result.Error!.Code);
    }

    /// <summary>
    /// Verifies reachable empty foreach loops complete through the runtime loop boundary.
    /// </summary>
    [Fact]
    public async Task ExecutesReachableEmptyForEachLoop()
    {
        WorkflowDocument workflow = Workflow(
            [Start(), new("loop", "flow.foreach", 1, parameters: new JsonObject { ["items"] = new JsonArray() }), Return("done")],
            [Connect("start", "main", "loop", "main"), Connect("loop", "completed", "done", "main")]);

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(workflow));

        Assert.True(result.Result.Status == WorkflowExecutionStatus.Succeeded, result.Result.Error?.Code + ": " + result.Result.Error?.Message);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "loop" && node.Status == NodeExecutionStatus.Succeeded);
    }

    /// <summary>
    /// Verifies repeat loop body nodes execute once per iteration with active iteration context.
    /// </summary>
    [Fact]
    public async Task ExecutesRepeatLoopBodyWithIterationContext()
    {
        LoopRecordingHandler handler = new("demo.loop-body");
        WorkflowNodeDefinition bodyDefinition = ControlNode("demo.loop-body");
        WorkflowDocument workflow = Workflow(
            [Start(), new("loop", "flow.repeat", 1, parameters: new JsonObject { ["count"] = 3 }), new("body", "demo.loop-body", 1), Return("done")],
            [Connect("start", "main", "loop", "main"), Connect("loop", "body", "body", "main"), Connect("body", "next", "loop", "continue"), Connect("loop", "completed", "done", "main")]);

        WorkflowRuntimeResult result = await Runtime(Catalog(bodyDefinition), [new CoreStartHandler(), new CoreReturnHandler(), handler]).ExecuteAsync(Request(workflow));

        Assert.True(result.Result.Status == WorkflowExecutionStatus.Succeeded, result.Result.Error?.Code + ": " + result.Result.Error?.Message);
        Assert.Equal([1L, 2L, 3L], handler.Iterations);
        Assert.Equal(3, result.NodeResults.Count(static node => node.NodeId == "body" && node.Status == NodeExecutionStatus.Succeeded));
    }

    /// <summary>
    /// Verifies workflow.invoke resolves a child workflow from a host-supplied repository and propagates a result object.
    /// </summary>
    [Fact]
    public async Task ExecutesWorkflowInvokeFromRepository()
    {
        WorkflowDocument child = Workflow([Start(), Return("child-done")], [Connect("start", "main", "child-done", "main")]);
        WorkflowDocument parent = Workflow(
            [Start(), new("invoke", "workflow.invoke", 1, parameters: new JsonObject { ["workflow"] = new JsonObject { ["id"] = "workflow" } }), Return("done")],
            [Connect("start", "main", "invoke", "main"), Connect("invoke", "result", "done", "main")]);
        DefaultWorkflowRuntime runtime = new(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            BuiltInWorkflowNodeCatalog.Catalog,
            BuiltInRuntimeHandlers.CreateResolver(),
            workflowRepository: ImmutableWorkflowRepository.FromDocuments(child));

        WorkflowRuntimeResult result = await runtime.ExecuteAsync(Request(parent));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        JsonObject invokeResult = Assert.IsType<JsonObject>(result.NodeResults.Single(static node => node.NodeId == "invoke").Outputs["result"]);
        Assert.Equal("Succeeded", invokeResult["status"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies StartAsync can suspend for interaction and resume from an in-memory continuation.
    /// </summary>
    [Fact]
    public async Task StartAsyncSuspendsAndResumesInteractionRequest()
    {
        WorkflowDocument workflow = Workflow(
            [Start(), new("ask", "interaction.request", 1, parameters: new JsonObject { ["kind"] = "confirmation", ["prompt"] = "Continue?" }), Return("done")],
            [Connect("start", "main", "ask", "main"), Connect("ask", "result", "done", "main")]);

        await using IWorkflowExecutionSession session = await new DefaultWorkflowRuntime().StartAsync(Request(workflow));
        PendingWorkflowInteraction pending = await WaitForPendingInteractionAsync(session);
        WorkflowInteractionContinuationResult continuation = await session.ContinueAsync(new WorkflowInteractionContinuation(pending.ContinuationId, value: JsonValue.Create(true)));
        WorkflowRuntimeResult result = await session.WaitForCompletionAsync();

        Assert.True(continuation.Accepted);
        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "ask" && node.Status == NodeExecutionStatus.Succeeded);
    }

    /// <summary>
    /// Verifies unreachable unsupported nodes do not fail a completed explicit return path.
    /// </summary>
    [Fact]
    public async Task DoesNotFailForUnreachableUnsupportedNode()
    {
        WorkflowDocument workflow = Workflow(
            [Start(), Return("done"), new("boundary", "workflow.invoke", 1, parameters: new JsonObject { ["workflow"] = new JsonObject { ["id"] = "child" } })],
            [Connect("start", "main", "done", "main")]);

        WorkflowRuntimeResult result = await new DefaultWorkflowRuntime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "boundary" && node.Status == NodeExecutionStatus.Skipped);
    }

    /// <summary>
    /// Verifies workflow documents and request input JSON are not mutated by execution.
    /// </summary>
    [Fact]
    public async Task DoesNotMutateWorkflowOrInputs()
    {
        JsonObject input = new() { ["value"] = 1 };
        WorkflowDocument workflow = Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")]);
        WorkflowExecutionRequest request = new(workflow, "execution", "plan", new Dictionary<string, JsonNode?> { ["input"] = input });

        await new DefaultWorkflowRuntime().ExecuteAsync(request);

        Assert.Equal(2, workflow.Nodes.Count);
        Assert.Equal(1, input["value"]!.GetValue<int>());
    }

    /// <summary>
    /// Verifies one runtime instance supports concurrent independent executions.
    /// </summary>
    [Fact]
    public async Task SupportsConcurrentIndependentExecutions()
    {
        DefaultWorkflowRuntime runtime = new();
        WorkflowDocument workflow = Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")]);

        WorkflowRuntimeResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => runtime.ExecuteAsync(new WorkflowExecutionRequest(workflow, "execution-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "plan")).AsTask()));

        Assert.All(results, static result => Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status));
        Assert.Equal(8, results.Select(static result => result.Result.ExecutionId).Distinct(StringComparer.Ordinal).Count());
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow, IWorkflowEventSink? sink = null)
    {
        return new WorkflowExecutionRequest(workflow, "execution", "plan", eventSink: sink);
    }

    private static DefaultWorkflowRuntime Runtime(IWorkflowNodeDefinitionCatalog catalog, IReadOnlyList<INodeHandler> handlers, INodeHandlerResolver? resolver = null)
    {
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            resolver ?? new ImmutableNodeHandlerResolver(handlers),
            new NodeParameterMaterializer(),
            new FakeClock(),
            new WorkflowRuntimeOptions());
    }

    private static WorkflowNodeDefinitionCatalog Catalog(params WorkflowNodeDefinition[] definitions)
    {
        return new WorkflowNodeDefinitionCatalog([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. definitions]);
    }

    private static WorkflowNodeDefinition ControlNode(string type)
    {
        return new(type, 1, inputs: Ports(WorkflowPortDirection.Input, "main"), outputs: Ports(WorkflowPortDirection.Output, "next"));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name, IReadOnlyList<string>? roles = null)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction, roles: roles),
        };
    }

    private static WorkflowDocument Workflow(IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<WorkflowConnection> connections)
    {
        return new(id: "workflow", name: "Workflow", nodes: nodes, connections: connections);
    }

    private static WorkflowNode Start()
    {
        return new("start", "core.start", 1);
    }

    private static WorkflowNode Return(string id)
    {
        return new(id, "core.return", 1, parameters: new JsonObject { ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" } });
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private static async ValueTask<PendingWorkflowInteraction> WaitForPendingInteractionAsync(IWorkflowExecutionSession session)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            IReadOnlyList<PendingWorkflowInteraction> pending = await session.GetPendingInteractionsAsync();
            if (pending.Count > 0)
            {
                return pending[0];
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException("Timed out waiting for a pending interaction.");
    }

    private sealed class FakeClock : IWorkflowClock
    {
        private long _ticks = new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero).Ticks;

        public DateTimeOffset UtcNow => new(Interlocked.Add(ref _ticks, TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }

    private sealed class RecordingSink : IWorkflowEventSink
    {
        private readonly object _gate = new();
        private readonly List<WorkflowEvent> _events = [];

        public IReadOnlyList<WorkflowEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return _events.ToArray();
                }
            }
        }

        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _events.Add(workflowEvent);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingPlanner : IWorkflowExecutionPlanner
    {
        public WorkflowExecutionPlanResult Plan(WorkflowDocument workflow, WorkflowAnalysisResult analysis)
        {
            return new WorkflowExecutionPlanResult(
                workflow.Id,
                WorkflowExecutionPlanStatus.Blocked,
                issues: [new WorkflowExecutionPlanIssue("TESTPLAN", WorkflowExecutionPlanIssueSeverity.Error, "Blocked.", string.Empty)]);
        }
    }

    private sealed class MismatchResolver(INodeHandler handler) : INodeHandlerResolver
    {
        public bool TryResolve(WorkflowNodeDefinitionKey definition, out INodeHandler? resolved)
        {
            resolved = definition.Type == "demo.mismatch" ? handler : BuiltInRuntimeHandlers.CreateResolver().TryResolve(definition, out INodeHandler? builtIn) ? builtIn : null;
            return resolved is not null;
        }
    }

    private abstract class TestHandler(string type) : INodeHandler
    {
        public virtual WorkflowNodeDefinitionKey Definition { get; } = new(type, 1);

        public abstract ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default);
    }

    private sealed class WrongIdentityHandler() : TestHandler("demo.other")
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }

    private sealed class BadControlHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["missing"])));
        }
    }

    private sealed class BadDataHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(dataOutputs: new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
            {
                ["missing"] = new([JsonValue.Create(1)]),
            })));
        }
    }

    private sealed class ExpectedFailureHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Failure(new WorkflowError("TEST1001", "Expected failure.", request.Identity.NodeId)));
        }
    }

    private sealed class ThrowingHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Boom.");
        }
    }

    private sealed class CancellingHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["main"])));
        }
    }

    private sealed class LoopRecordingHandler(string type) : TestHandler(type)
    {
        private readonly List<long> _iterations = [];

        public IReadOnlyList<long> Iterations => _iterations;

        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _iterations.Add(request.Iterations["loop"].Number);
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }
}
