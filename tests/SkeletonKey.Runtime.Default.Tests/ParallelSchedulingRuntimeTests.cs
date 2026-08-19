using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.BuiltIns.Runtime;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Planning.Default;
using SkeletonKey.Runtime;
using SkeletonKey.Runtime.Default;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;

namespace SkeletonKey.Runtime.Default.Tests;

/// <summary>Covers bounded in-process parallel scheduling.</summary>
public sealed class ParallelSchedulingRuntimeTests
{
    /// <summary>Verifies independent ready handler steps run concurrently and respect the runtime cap.</summary>
    [Fact]
    public async Task ExecutesIndependentReadyStepsWithBoundedConcurrency()
    {
        ConcurrencyProbeHandler probe = new();
        WorkflowNodeDefinition fork = Definition("demo.fork", ["main"], ["left", "right"]);
        WorkflowNodeDefinition probeDefinition = Definition("demo.probe", ["main"], ["next"]);
        WorkflowNodeDefinition join = Definition("demo.join", ["left", "right"], ["next"]);
        WorkflowDocument workflow = new(
            id: "parallel-branches",
            name: "Parallel Branches",
            nodes:
            [
                new("start", "core.start", 1),
                new("fork", "demo.fork", 1),
                new("left", "demo.probe", 1),
                new("right", "demo.probe", 1),
                new("join", "demo.join", 1),
                Return(),
            ],
            connections:
            [
                Connect("start", "main", "fork", "main"),
                Connect("fork", "left", "left", "main"),
                Connect("fork", "right", "right", "main"),
                Connect("left", "next", "join", "left"),
                Connect("right", "next", "join", "right"),
                Connect("join", "next", "done", "main"),
            ]);

        WorkflowRuntimeResult result = await Runtime(
            [fork, probeDefinition, join],
            [new ForkHandler(), probe, new JoinHandler()],
            maximumParallelSteps: 2).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal(2, probe.ExecutionCount);
        Assert.Equal(2, probe.MaximumConcurrency);
        Assert.Equal(["left", "right"], result.NodeResults.Where(static item => item.NodeId is "left" or "right").Select(static item => item.NodeId));
    }

    /// <summary>Verifies parallel foreach honors both its declaration and the runtime-wide concurrency cap.</summary>
    [Fact]
    public async Task ExecutesForEachIterationsWithDeclaredBoundedConcurrency()
    {
        ConcurrencyProbeHandler probe = new();
        WorkflowNodeDefinition probeDefinition = Definition("demo.probe", ["main"], ["next"]);
        WorkflowDocument workflow = new(
            id: "parallel-foreach",
            name: "Parallel ForEach",
            nodes:
            [
                new("start", "core.start", 1),
                new(
                    "loop",
                    "flow.foreach",
                    1,
                    parameters: new JsonObject
                    {
                        ["items"] = new JsonArray(1, 2, 3, 4),
                        ["execution"] = new JsonObject
                        {
                            ["mode"] = "parallel",
                            ["maxConcurrency"] = 3,
                        },
                    }),
                new("body", "demo.probe", 1),
                Return(),
            ],
            connections:
            [
                Connect("start", "main", "loop", "main"),
                Connect("loop", "body", "body", "main"),
                Connect("body", "next", "loop", "continue"),
                Connect("loop", "completed", "done", "main"),
            ]);

        WorkflowRuntimeResult result = await Runtime(
            [probeDefinition],
            [probe],
            maximumParallelSteps: 2).ExecuteAsync(Request(workflow));

        Assert.Equal(WorkflowExecutionStatus.Succeeded, result.Result.Status);
        Assert.Equal(4, probe.ExecutionCount);
        Assert.Equal(2, probe.MaximumConcurrency);
        Assert.Equal(4, result.NodeResults.Count(static item => item.NodeId == "body" && item.Status == NodeExecutionStatus.Succeeded));
    }

    private static DefaultWorkflowRuntime Runtime(
        IReadOnlyList<WorkflowNodeDefinition> definitions,
        IReadOnlyList<INodeHandler> handlers,
        int maximumParallelSteps)
    {
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, .. definitions]);
        return new DefaultWorkflowRuntime(
            new WorkflowSemanticValidator(),
            new DefaultWorkflowAnalyzer(),
            new DefaultWorkflowExecutionPlanner(),
            catalog,
            new ImmutableNodeHandlerResolver([.. BuiltInRuntimeHandlers.Create(), .. handlers]),
            options: new WorkflowRuntimeOptions(maximumParallelSteps: maximumParallelSteps));
    }

    private static WorkflowExecutionRequest Request(WorkflowDocument workflow)
    {
        return new WorkflowExecutionRequest(workflow, "parallel-execution", "parallel-plan");
    }

    private static WorkflowNodeDefinition Definition(string type, string[] inputs, string[] outputs)
    {
        return new(
            type,
            1,
            inputs: inputs.ToDictionary(static name => name, static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Input), StringComparer.Ordinal),
            outputs: outputs.ToDictionary(static name => name, static name => new WorkflowPortDefinition(name, WorkflowPortDirection.Output), StringComparer.Ordinal));
    }

    private static WorkflowNode Return()
    {
        return new(
            "done",
            "core.return",
            1,
            parameters: new JsonObject
            {
                ["outcome"] = new JsonObject
                {
                    ["kind"] = "success",
                    ["code"] = "done",
                },
            });
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private sealed class ForkHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.fork", 1);

        public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["left", "right"])));
        }
    }

    private sealed class JoinHandler : INodeHandler
    {
        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.join", 1);

        public ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NodeHandlerResult.Success(new NodeHandlerOutputs(["next"])));
        }
    }

    private sealed class ConcurrencyProbeHandler : INodeHandler
    {
        private int _active;
        private int _executions;
        private int _maximumConcurrency;

        public WorkflowNodeDefinitionKey Definition { get; } = new("demo.probe", 1);

        public int ExecutionCount => Volatile.Read(ref _executions);

        public int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

        public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executions);
            int active = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = Volatile.Read(ref _maximumConcurrency);
            }
            while (active > observed && Interlocked.CompareExchange(ref _maximumConcurrency, active, observed) != observed);

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                return NodeHandlerResult.Success(new NodeHandlerOutputs(["next"]));
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
