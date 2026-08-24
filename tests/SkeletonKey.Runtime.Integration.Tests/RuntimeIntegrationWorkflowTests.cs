using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Events;
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
using SkeletonKey.Runtime.Default;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Runtime.Integration.Tests;

/// <summary>
/// Covers executable end-to-end workflows over the default runtime.
/// </summary>
public sealed class RuntimeIntegrationWorkflowTests
{
    /// <summary>
    /// Verifies a basic start-to-return workflow succeeds and emits ordered events.
    /// </summary>
    [Fact]
    public async Task BasicReturnWorkflowSucceeds()
    {
        RecordingSink sink = new();
        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(Workflow([Start(), Return("done")], [Connect("start", "main", "done", "main")]), sink));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal("done", result.Result.Outcome!.Code);
        Assert.Equal(["start", "done"], result.NodeResults.Where(static node => node.Status == NodeExecutionStatus.Succeeded).Select(static node => node.NodeId));
        Assert.Equal(sink.RuntimeEvents.Select(static item => item.Sequence), sink.RuntimeEvents.OrderBy(static item => item.Sequence).Select(static item => item.Sequence));
    }

    /// <summary>
    /// Verifies a structural core.end terminal executes successfully through the default runtime.
    /// </summary>
    [Fact]
    public async Task BasicEndWorkflowSucceeds()
    {
        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(Workflow([Start(), End()], [Connect("start", "main", "end", "main")])));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Null(result.Result.Outcome);
        Assert.Null(result.Result.Error);
        Assert.Equal(["start", "end"], result.NodeResults.Where(static node => node.Status == NodeExecutionStatus.Succeeded).Select(static node => node.NodeId));
    }

    /// <summary>
    /// Verifies flow.if executes the true branch and skips the false branch.
    /// </summary>
    [Fact]
    public async Task ConditionalWorkflowExecutesTrueBranch()
    {
        WorkflowDocument workflow = Workflow(
            [Start(), new("check", "flow.if", 1, parameters: new JsonObject { ["condition"] = true }), Return("trueReturn"), Return("falseReturn")],
            [Connect("start", "main", "check", "main"), Connect("check", "true", "trueReturn", "main"), Connect("check", "false", "falseReturn", "main")]);

        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "trueReturn" && node.Status == NodeExecutionStatus.Succeeded);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "falseReturn" && node.Status == NodeExecutionStatus.Skipped);
    }

    /// <summary>
    /// Verifies flow.if executes the false branch and skips the true branch.
    /// </summary>
    [Fact]
    public async Task ConditionalWorkflowExecutesFalseBranch()
    {
        WorkflowDocument workflow = Workflow(
            [Start(), new("check", "flow.if", 1, parameters: new JsonObject { ["condition"] = false }), Return("trueReturn"), Return("falseReturn")],
            [Connect("start", "main", "check", "main"), Connect("check", "true", "trueReturn", "main"), Connect("check", "false", "falseReturn", "main")]);

        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "falseReturn" && node.Status == NodeExecutionStatus.Succeeded);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "trueReturn" && node.Status == NodeExecutionStatus.Skipped);
    }

    /// <summary>
    /// Verifies flow.switch executes a matching dynamic case.
    /// </summary>
    [Fact]
    public async Task SwitchWorkflowExecutesMatchingCase()
    {
        WorkflowDocument workflow = SwitchWorkflow(true);

        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "matchedReturn" && node.Status == NodeExecutionStatus.Succeeded);
    }

    /// <summary>
    /// Verifies flow.switch executes default when no case matches.
    /// </summary>
    [Fact]
    public async Task SwitchWorkflowExecutesDefaultCase()
    {
        WorkflowDocument workflow = SwitchWorkflow(false);

        WorkflowRuntimeResult result = await Runtime().ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Contains(result.NodeResults, static node => node.NodeId == "defaultReturn" && node.Status == NodeExecutionStatus.Succeeded);
    }

    /// <summary>
    /// Verifies data outputs propagate into later binding and expression materialization.
    /// </summary>
    [Fact]
    public async Task DataDependencyWorkflowMaterializesBindingAndExpression()
    {
        CapturingEchoHandler echo = new();
        WorkflowNodeDefinition sourceDefinition = new(
            "demo.source",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Merge(Ports(WorkflowPortDirection.Output, "next"), Ports(WorkflowPortDirection.Output, "value", ["data"])));
        WorkflowNodeDefinition echoDefinition = new(
            "demo.echo",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Merge(Ports(WorkflowPortDirection.Output, "next"), Ports(WorkflowPortDirection.Output, "result", ["data"])));
        WorkflowNode echoNode = new("echo", "demo.echo", 1, parameters: new JsonObject
        {
            ["fromBinding"] = new JsonObject { ["$binding"] = new JsonObject { ["source"] = "node", ["node"] = "source", ["port"] = "value" } },
            ["fromExpression"] = new JsonObject { ["$expression"] = "nodes['source'].outputs['value']" },
        });
        WorkflowDocument workflow = Workflow(
            [Start(), new("source", "demo.source", 1), echoNode, Return("done")],
            [Connect("start", "main", "source", "main"), Connect("source", "next", "echo", "main"), Connect("echo", "next", "done", "main")],
            outputs: new Dictionary<string, WorkflowOutputDefinition>(StringComparer.Ordinal)
            {
                ["materialized"] = new(WorkflowOutputMode.Single, new WorkflowEndpoint("echo", "result")),
            });

        WorkflowRuntimeResult result = await Runtime(Catalog(sourceDefinition, echoDefinition), [new CoreStartHandler(), new CoreReturnHandler(), new SourceHandler(), echo]).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal("payload", echo.LastParameters!["fromBinding"]!.GetValue<string>());
        Assert.Equal("payload", echo.LastParameters["fromExpression"]!.GetValue<string>());
        Assert.Equal("payload", result.Result.Outputs["materialized"]!["fromBinding"]!.GetValue<string>());
    }

    /// <summary>
    /// Verifies explicit JSON null data outputs are preserved.
    /// </summary>
    [Fact]
    public async Task PreservesExplicitJsonNullDataOutput()
    {
        WorkflowNodeDefinition node = DataNode("demo.null", allowsMultiple: false);
        WorkflowDocument workflow = Workflow(
            [Start(), new("data", "demo.null", 1), Return("done")],
            [Connect("start", "main", "data", "main"), Connect("data", "next", "done", "main")],
            outputs: new Dictionary<string, WorkflowOutputDefinition>(StringComparer.Ordinal) { ["value"] = new(WorkflowOutputMode.Single, new WorkflowEndpoint("data", "value")) });

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new CoreReturnHandler(), new NullDataHandler("demo.null")]).ExecuteAsync(Request(workflow));

        Assert.True(result.Result.Outputs.ContainsKey("value"));
        Assert.Null(result.Result.Outputs["value"]);
    }

    /// <summary>
    /// Verifies multi-value output order is preserved for collection outputs.
    /// </summary>
    [Fact]
    public async Task PreservesMultiValueOutputOrder()
    {
        WorkflowNodeDefinition node = DataNode("demo.multi", allowsMultiple: true);
        WorkflowDocument workflow = Workflow(
            [Start(), new("data", "demo.multi", 1), Return("done")],
            [Connect("start", "main", "data", "main"), Connect("data", "next", "done", "main")],
            outputs: new Dictionary<string, WorkflowOutputDefinition>(StringComparer.Ordinal) { ["values"] = new(WorkflowOutputMode.Collection, new WorkflowEndpoint("data", "value")) });

        WorkflowRuntimeResult result = await Runtime(Catalog(node), [new CoreStartHandler(), new CoreReturnHandler(), new MultiDataHandler("demo.multi")]).ExecuteAsync(Request(workflow));

        JsonArray values = result.Result.Outputs["values"]!.AsArray();
        Assert.Equal([1, 2, 3], values.Select(static item => item!.GetValue<int>()));
    }

    /// <summary>
    /// Verifies a deterministic 1,000-node graph executes once per node without recursion.
    /// </summary>
    [Fact]
    public async Task LargeWorkflowExecutesOneThousandSimpleNodes()
    {
        WorkflowNodeDefinition nodeDefinition = new("demo.large", 1, inputs: Ports(WorkflowPortDirection.Input, "main"), outputs: Ports(WorkflowPortDirection.Output, "next"));
        List<WorkflowNode> nodes = [Start()];
        List<WorkflowConnection> connections = [];
        string previous = "start";
        string previousPort = "main";
        for (int index = 1; index <= 1000; index++)
        {
            string nodeId = "node-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nodes.Add(new(nodeId, "demo.large", 1));
            connections.Add(Connect(previous, previousPort, nodeId, "main"));
            previous = nodeId;
            previousPort = "next";
        }

        nodes.Add(Return("done"));
        connections.Add(Connect(previous, previousPort, "done", "main"));

        WorkflowRuntimeResult result = await Runtime(Catalog(nodeDefinition), [new CoreStartHandler(), new CoreReturnHandler(), new LargeHandler()]).ExecuteAsync(Request(Workflow(nodes, connections)));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal(1002, result.NodeResults.Count(static node => node.Status == NodeExecutionStatus.Succeeded));
        Assert.Equal(1, result.NodeResults.Count(static node => node.NodeId == "node-1000"));
    }

    private static WorkflowDocument SwitchWorkflow(bool match)
    {
        return Workflow(
            [
                Start(),
                new("switch", "flow.switch", 1, parameters: new JsonObject
                {
                    ["cases"] = new JsonArray { new JsonObject { ["id"] = "matched", ["when"] = match } },
                }),
                Return("matchedReturn"),
                Return("defaultReturn"),
            ],
            [Connect("start", "main", "switch", "main"), Connect("switch", "matched", "matchedReturn", "main"), Connect("switch", "default", "defaultReturn", "main")]);
    }

    private static DefaultWorkflowRuntime Runtime(IWorkflowNodeDefinitionCatalog? catalog = null, IReadOnlyList<INodeHandler>? handlers = null)
    {
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog ?? BuiltInWorkflowNodeCatalog.Catalog,
            new ImmutableNodeHandlerResolver(handlers ?? BuiltInRuntimeHandlers.Create()),
            new NodeParameterMaterializer(),
            new FakeClock(),
            new WorkflowRuntimeOptions(maximumExecutedNodeAttempts: 2_000, maximumStoredNodeResults: 2_000));
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow, IWorkflowEventSink? sink = null)
    {
        return new WorkflowExecutionRequest(workflow, "execution", "plan", eventSink: sink);
    }

    private static WorkflowNodeDefinitionCatalog Catalog(params WorkflowNodeDefinition[] definitions)
    {
        return new WorkflowNodeDefinitionCatalog([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. definitions]);
    }

    private static WorkflowNodeDefinition DataNode(string type, bool allowsMultiple)
    {
        return new(
            type,
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            outputs: Merge(Ports(WorkflowPortDirection.Output, "next"), Ports(WorkflowPortDirection.Output, "value", ["data"], allowsMultiple)));
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name, IReadOnlyList<string>? roles = null, bool allowsMultiple = false)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction, allowsMultiple: allowsMultiple, roles: roles),
        };
    }

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Merge(params IReadOnlyDictionary<string, WorkflowPortDefinition>[] dictionaries)
    {
        Dictionary<string, WorkflowPortDefinition> merged = new(StringComparer.Ordinal);
        foreach (IReadOnlyDictionary<string, WorkflowPortDefinition> dictionary in dictionaries)
        {
            foreach (KeyValuePair<string, WorkflowPortDefinition> item in dictionary)
            {
                merged[item.Key] = item.Value;
            }
        }

        return merged;
    }

    private static WorkflowDocument Workflow(IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<WorkflowConnection> connections, IReadOnlyDictionary<string, WorkflowOutputDefinition>? outputs = null)
    {
        return new(id: "workflow", name: "Workflow", nodes: nodes, connections: connections, outputs: outputs);
    }

    private static WorkflowNode Start()
    {
        return new("start", "core.start", 1);
    }

    private static WorkflowNode End()
    {
        return new("end", "core.end", 1);
    }

    private static WorkflowNode Return(string id)
    {
        return new(id, "core.return", 1, parameters: new JsonObject { ["outcome"] = new JsonObject { ["kind"] = "success", ["code"] = "done" } });
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private sealed class FakeClock : IWorkflowClock
    {
        private long _ticks = new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero).Ticks;

        public DateTimeOffset UtcNow => new(Interlocked.Add(ref _ticks, TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }

    private sealed class RecordingSink : IWorkflowEventSink
    {
        private readonly List<WorkflowEvent> _events = [];

        public IReadOnlyList<RuntimeWorkflowEvent> RuntimeEvents => _events.OfType<RuntimeWorkflowEvent>().ToArray();

        public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
        {
            _events.Add(workflowEvent);
            return ValueTask.CompletedTask;
        }
    }

    private abstract class TestHandler(string type) : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new(type, 1);

        public abstract ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default);
    }

    private sealed class SourceHandler() : TestHandler("demo.source")
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(
                ["next"],
                new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
                {
                    ["value"] = new([JsonValue.Create("payload")]),
                })));
        }
    }

    private sealed class CapturingEchoHandler() : TestHandler("demo.echo")
    {
        public JsonObject? LastParameters { get; private set; }

        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            LastParameters = request.Parameters;
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(
                ["next"],
                new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
                {
                    ["result"] = new([request.Parameters]),
                })));
        }
    }

    private sealed class NullDataHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(
                ["next"],
                new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
                {
                    ["value"] = new([null]),
                })));
        }
    }

    private sealed class MultiDataHandler(string type) : TestHandler(type)
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(
                ["next"],
                new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
                {
                    ["value"] = new([JsonValue.Create(1), JsonValue.Create(2), JsonValue.Create(3)]),
                })));
        }
    }

    private sealed class LargeHandler() : TestHandler("demo.large")
    {
        public override ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }
}
