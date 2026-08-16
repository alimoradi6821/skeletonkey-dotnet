using System.Text.Json.Nodes;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.Catalog;
using SkeletonKey.Planning.Default;
using SkeletonKey.Workflow.Bindings;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Planning.Default.Tests;

/// <summary>
/// Covers the deterministic default workflow execution planner.
/// </summary>
public sealed class DefaultWorkflowExecutionPlannerTests
{
    /// <summary>
    /// Verifies invalid analysis and workflow identity mismatch block plan creation.
    /// </summary>
    [Fact]
    public void RejectsInvalidAnalysisAndWorkflowMismatch()
    {
        WorkflowDocument workflow = Workflow([Start()]);
        WorkflowAnalysisResult invalid = new(
            "other",
            issues: [new(WorkflowAnalysisCodes.UnknownNodeType, WorkflowAnalysisSeverity.Error, "Nope.", "/nodes/0")]);

        WorkflowExecutionPlanResult result = new DefaultWorkflowExecutionPlanner().Plan(workflow, invalid);

        Assert.False(result.IsReady);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowExecutionPlanCodes.AnalysisWorkflowMismatch);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowExecutionPlanCodes.AnalysisErrors);
    }

    /// <summary>
    /// Verifies steps, deterministic step IDs, node order, control dependencies, and return terminal boundaries.
    /// </summary>
    [Fact]
    public void CreatesStepsControlDependenciesEntryAndReturnTerminal()
    {
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("check", "flow.if", 1, parameters: new JsonObject { ["condition"] = true }),
                Return("done"),
            ],
            [
                Connect("start", "main", "check", "main"),
                Connect("check", "true", "done", "main"),
            ]);

        WorkflowExecutionPlanResult result = Plan(workflow, BuiltInWorkflowNodeCatalog.Catalog);

        Assert.True(result.IsReady);
        WorkflowExecutionPlan plan = result.Plan!;
        Assert.Equal(["node:start", "node:check", "node:done"], plan.Steps.Select(static step => step.StepId));
        Assert.Equal(["node:start"], plan.EntryStepIds);
        Assert.Equal(["node:done"], plan.TerminalStepIds);
        Assert.Contains(plan.Dependencies, static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Control && dependency.StepId == "node:check" && dependency.TargetStepId == "node:done");
        Assert.Equal(WorkflowExecutionPlanStepKind.Control, plan.Steps.Single(static step => step.NodeId == "check").Kind);
        Assert.NotNull(plan.Steps.Single(static step => step.NodeId == "done").ControlBoundary);
    }

    /// <summary>
    /// Verifies explicit graph data, binding-derived, and expression-derived dependencies are created and deduplicated.
    /// </summary>
    [Fact]
    public void CreatesDataBindingAndExpressionDependencies()
    {
        WorkflowNodeDefinition source = new("demo.source", 1, inputs: Ports(WorkflowPortDirection.Input, "main"), outputs: Ports(WorkflowPortDirection.Output, "data", roles: ["data"]));
        WorkflowNodeDefinition consumer = new("demo.consumer", 1, inputs: Ports(WorkflowPortDirection.Input, "data", roles: ["data"]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, source, consumer]);
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("source", "demo.source", 1),
                new("consumer", "demo.consumer", 1, parameters: new JsonObject
                {
                    ["binding"] = new JsonObject
                    {
                        ["$binding"] = new JsonObject { ["source"] = "node", ["node"] = "source", ["port"] = "data" },
                    },
                    ["expression"] = new JsonObject { ["$expression"] = "nodes['source'].outputs['data']" },
                }),
            ],
            [
                Connect("start", "main", "source", "main"),
                Connect("source", "data", "consumer", "data"),
            ]);

        WorkflowExecutionPlanResult result = Plan(workflow, catalog);

        Assert.True(result.IsReady);
        IReadOnlyList<WorkflowExecutionPlanDependency> dataDependencies = result.Plan!.Dependencies
            .Where(static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Data && dependency.StepId == "node:source" && dependency.TargetStepId == "node:consumer")
            .ToArray();
        Assert.Equal(2, dataDependencies.Count);
        Assert.Contains(dataDependencies, static dependency => dependency.TargetPort == "data");
        Assert.Contains(dataDependencies, static dependency => dependency.SourcePath == "/nodes/2/parameters/binding");
        Assert.Contains(result.Plan.Steps.Single(static step => step.NodeId == "consumer").DependsOn, static dependency => dependency.Kind == WorkflowExecutionPlanDependencyKind.Data);
    }

    /// <summary>
    /// Verifies resource-use planning preserves slot, resource, access, kind, and capabilities without resolving live resources.
    /// </summary>
    [Fact]
    public void CreatesResourceUseDeclarations()
    {
        WorkflowNodeDefinition node = new(
            "demo.browser",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["browser"] = new("browser", StandardWorkflowResourceKinds.WebBrowser, capabilities: [StandardWorkflowResourceCapabilities.WebFrames]),
            });
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, node]);
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("use", "demo.browser", 1, parameters: new JsonObject { ["browser"] = new JsonObject { ["$resource"] = new JsonObject { ["name"] = "browser" } } }),
            ],
            [Connect("start", "main", "use", "main")],
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["browser"] = new(StandardWorkflowResourceKinds.WebBrowser, WorkflowResourceLifetime.Invocation, WorkflowResourceAccessMode.Exclusive, capabilities: [StandardWorkflowResourceCapabilities.WebFrames]),
            });

        WorkflowExecutionPlanResult result = Plan(workflow, catalog);

        Assert.True(result.IsReady);
        WorkflowExecutionPlanResourceUse use = Assert.Single(result.Plan!.Steps.Single(static step => step.NodeId == "use").Resources);
        Assert.Equal("browser", use.ResourceName);
        Assert.Equal(WorkflowResourceAccessMode.Exclusive, use.Access);
        Assert.Equal([StandardWorkflowResourceCapabilities.WebFrames], use.Capabilities);
    }

    /// <summary>
    /// Verifies loop boundaries preserve loop metadata and valid loop back edges are not rejected as cycles.
    /// </summary>
    [Fact]
    public void PreservesLoopBackEdgeWithoutUnrolling()
    {
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("loop", "flow.foreach", 1, parameters: new JsonObject { ["items"] = new JsonArray() }),
                Return("done"),
            ],
            [
                Connect("start", "main", "loop", "main"),
                Connect("loop", "body", "loop", "continue"),
                Connect("loop", "completed", "done", "main"),
            ]);

        WorkflowExecutionPlanResult result = Plan(workflow, BuiltInWorkflowNodeCatalog.Catalog);

        Assert.True(result.IsReady);
        WorkflowExecutionPlanStep loop = result.Plan!.Steps.Single(static step => step.NodeId == "loop");
        Assert.Equal(WorkflowExecutionPlanStepKind.Loop, loop.Kind);
        Assert.NotNull(loop.LoopBoundary);
        Assert.Equal("loop", loop.LoopBoundary!.Metadata["iterationId"]);
        Assert.Equal(3, result.Plan.Steps.Count);
    }

    /// <summary>
    /// Verifies unstructured dependency cycles block planning deterministically.
    /// </summary>
    [Fact]
    public void RejectsUnstructuredDataCycle()
    {
        WorkflowNodeDefinition node = new(
            "demo.node",
            1,
            inputs: new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
            {
                ["main"] = new("main", WorkflowPortDirection.Input),
                ["data"] = new("data", WorkflowPortDirection.Input, roles: ["data"]),
            },
            outputs: Ports(WorkflowPortDirection.Output, "out", roles: ["data"]));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, node]);
        WorkflowDocument workflow = Workflow(
            [Start(), new("a", "demo.node", 1), new("b", "demo.node", 1)],
            [
                Connect("start", "main", "a", "main"),
                Connect("a", "out", "b", "data"),
                Connect("b", "out", "a", "data"),
            ]);

        WorkflowExecutionPlanResult result = Plan(workflow, catalog);

        Assert.False(result.IsReady);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowExecutionPlanCodes.DependencyCycle);
    }

    /// <summary>
    /// Verifies invocation and interaction boundaries remain opaque and do not load children or call handlers.
    /// </summary>
    [Fact]
    public void CreatesInvocationAndInteractionBoundaries()
    {
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("invoke", "workflow.invoke", 1, parameters: new JsonObject { ["workflow"] = new JsonObject { ["id"] = "child", ["version"] = "1.0.0" } }),
                new("request", "interaction.request", 1, parameters: new JsonObject
                {
                    ["kind"] = "confirmation",
                    ["prompt"] = "Continue?",
                }),
                Return("done"),
            ],
            [
                Connect("start", "main", "invoke", "main"),
                Connect("invoke", "result", "request", "main"),
                Connect("request", "result", "done", "main"),
            ],
            resources: new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["interaction"] = new(StandardWorkflowResourceKinds.InteractionHandler, capabilities: [StandardWorkflowResourceCapabilities.InteractionConfirmation]),
            });

        WorkflowExecutionPlanResult result = Plan(workflow, BuiltInWorkflowNodeCatalog.Catalog);

        Assert.True(result.IsReady);
        WorkflowExecutionPlanStep invoke = result.Plan!.Steps.Single(static step => step.NodeId == "invoke");
        WorkflowExecutionPlanStep request = result.Plan.Steps.Single(static step => step.NodeId == "request");
        Assert.Equal("child", invoke.InvocationBoundary!.Metadata["workflowId"]);
        Assert.Equal("1.0.0", invoke.InvocationBoundary.Metadata["workflowVersion"]);
        Assert.True(request.MaySuspend);
        Assert.NotNull(request.ControlBoundary);
    }

    /// <summary>
    /// Verifies planning handles large workflows iteratively and returns deterministic results.
    /// </summary>
    [Fact]
    public void IsDeterministicThreadSafeAndHandlesLargeWorkflows()
    {
        WorkflowNodeDefinition node = new(
            "demo.large",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main", allowsMultiple: true),
            outputs: Ports(WorkflowPortDirection.Output, "next", allowsMultiple: true));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, node]);
        WorkflowDocument workflow = LargeWorkflow(1000, 2000);
        DefaultWorkflowAnalyzer analyzer = new();
        WorkflowAnalysisResult analysis = analyzer.Analyze(workflow, catalog);
        DefaultWorkflowExecutionPlanner planner = new();

        WorkflowExecutionPlanResult[] results = Enumerable.Range(0, 4)
            .AsParallel()
            .Select(_ => planner.Plan(workflow, analysis))
            .ToArray();

        Assert.All(results, static result => Assert.True(result.IsReady));
        Assert.All(results, static result => Assert.Equal(1000, result.Plan!.Steps.Count));
        Assert.All(results, static result => Assert.Equal(2000, result.Plan!.Dependencies.Count));
        Assert.All(results, result => Assert.Equal(results[0].Plan!.PlanId, result.Plan!.PlanId));
    }

    private static WorkflowExecutionPlanResult Plan(WorkflowDocument workflow, IWorkflowNodeDefinitionCatalog catalog)
    {
        WorkflowAnalysisResult analysis = new DefaultWorkflowAnalyzer().Analyze(workflow, catalog);
        return new DefaultWorkflowExecutionPlanner().Plan(workflow, analysis);
    }

    private static WorkflowDocument LargeWorkflow(int nodeCount, int connectionCount)
    {
        List<WorkflowNode> nodes = [Start()];
        for (int index = 1; index < nodeCount; index++)
        {
            nodes.Add(new($"node-{index}", "demo.large", 1));
        }

        List<WorkflowConnection> connections = [];
        int count = 0;
        for (int distance = 1; count < connectionCount && distance < nodeCount; distance++)
        {
            for (int source = 0; count < connectionCount && source + distance < nodeCount; source++)
            {
                int target = source + distance;
                connections.Add(Connect(source == 0 ? "start" : $"node-{source}", source == 0 ? "main" : "next", $"node-{target}", "main"));
                count++;
            }
        }

        return Workflow(nodes, connections);
    }

    private static WorkflowDocument Workflow(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection>? connections = null,
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null,
        IReadOnlyDictionary<string, WorkflowOutputDefinition>? outputs = null)
    {
        return new(id: "workflow", name: "Workflow", nodes: nodes, connections: connections, resources: resources, outputs: outputs);
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

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(
        WorkflowPortDirection direction,
        string name,
        IReadOnlyList<string>? roles = null,
        bool allowsMultiple = false)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction, allowsMultiple: allowsMultiple, roles: roles),
        };
    }
}
