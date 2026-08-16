using System.Text.Json.Nodes;
using SkeletonKey.Analysis;
using SkeletonKey.Analysis.Default;
using SkeletonKey.BuiltIns;
using SkeletonKey.Catalog;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Analysis.Default.Tests;

/// <summary>
/// Covers the deterministic default catalog-aware workflow analyzer.
/// </summary>
public sealed class DefaultWorkflowAnalyzerTests
{
    /// <summary>
    /// Verifies exact definition resolution, static ports, dynamic switch ports, and deterministic ordering.
    /// </summary>
    [Fact]
    public void ResolvesDefinitionsStaticAndDynamicPortsInDocumentOrder()
    {
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("switch", "flow.switch", 1, parameters: new JsonObject
                {
                    ["cases"] = new JsonArray
                    {
                        new JsonObject { ["id"] = "phone", ["when"] = true },
                        new JsonObject { ["id"] = "email", ["when"] = false },
                    },
                }),
                Return("done"),
            ],
            [
                Connect("start", "main", "switch", "main"),
                Connect("switch", "phone", "done", "main"),
            ]);

        WorkflowAnalysisResult result = new DefaultWorkflowAnalyzer().Analyze(workflow, BuiltInWorkflowNodeCatalog.Catalog);

        Assert.True(result.CanPlanExecution);
        Assert.Equal(["start", "switch", "done"], result.Nodes.Select(static node => node.NodeId));
        WorkflowNodeAnalysis switchNode = result.Nodes.Single(static node => node.NodeId == "switch");
        Assert.Equal(WorkflowNodeCatalogStatus.Known, switchNode.CatalogStatus);
        Assert.Equal(["main", "default", "phone", "email"], switchNode.EffectivePorts.Select(static port => port.Id));
        Assert.Equal(WorkflowEffectivePortOrigin.Dynamic, switchNode.EffectivePorts.Single(static port => port.Id == "phone").Origin);
        Assert.All(result.Connections, connection => Assert.Equal(WorkflowConnectionRoleCompatibilityStatus.Compatible, connection.RoleCompatibilityStatus));
    }

    /// <summary>
    /// Verifies unknown type, unknown version, deprecated definitions, and parameter-contract diagnostics.
    /// </summary>
    [Fact]
    public void ReportsDefinitionAndParameterIssues()
    {
        WorkflowNodeDefinition deprecated = new(
            "demo.old",
            1,
            inputs: Ports(WorkflowPortDirection.Input, "main"),
            deprecation: new WorkflowNodeDeprecationMetadata(true, message: "Use demo.new."));
        WorkflowNodeDefinition needsParameter = new(
            "demo.needs",
            1,
            parametersSchema: new JsonObject { ["required"] = new JsonArray("value") },
            inputs: Ports(WorkflowPortDirection.Input, "main"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, deprecated, needsParameter]);
        WorkflowDocument workflow = Workflow(
            [Start(), new("unknown", "demo.missing", 1), new("version", "demo.old", 2), new("old", "demo.old", 1), new("needs", "demo.needs", 1)],
            [Connect("start", "main", "old", "main"), Connect("old", "main", "needs", "main")]);

        WorkflowAnalysisResult result = new DefaultWorkflowAnalyzer().Analyze(workflow, catalog);

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.UnknownNodeType);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.UnknownNodeVersion);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.DeprecatedNodeDefinition && issue.Severity == WorkflowAnalysisSeverity.Warning);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.InvalidNodeParameters);
    }

    /// <summary>
    /// Verifies dynamic-port validation rejects duplicate and static-conflicting derived IDs.
    /// </summary>
    [Fact]
    public void RejectsInvalidDynamicPortDeclarations()
    {
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("switch", "flow.switch", 1, parameters: new JsonObject
                {
                    ["cases"] = new JsonArray
                    {
                        new JsonObject { ["id"] = "default", ["when"] = true },
                        new JsonObject { ["id"] = "phone", ["when"] = false },
                        new JsonObject { ["id"] = "phone", ["when"] = true },
                    },
                }),
            ],
            [Connect("start", "main", "switch", "main")]);

        WorkflowAnalysisResult result = new DefaultWorkflowAnalyzer().Analyze(workflow, BuiltInWorkflowNodeCatalog.Catalog);

        Assert.True(result.Issues.Count(issue => issue.Code == WorkflowAnalysisCodes.InvalidDynamicPortDeclaration) >= 2);
        Assert.Equal(WorkflowDynamicPortAnalysisStatus.NotDynamic, result.Connections[0].DynamicPortStatus);
    }

    /// <summary>
    /// Verifies endpoint direction, role compatibility, unknown ports, and multiplicity analysis.
    /// </summary>
    [Fact]
    public void AnalyzesConnectionEndpointsRolesAndMultiplicity()
    {
        WorkflowNodeDefinition source = new("demo.source", 1, inputs: Ports(WorkflowPortDirection.Input, "main"), outputs: Ports(WorkflowPortDirection.Output, "data", roles: ["data"]));
        WorkflowNodeDefinition target = new("demo.target", 1, inputs: Ports(WorkflowPortDirection.Input, "data", roles: ["data"]));
        WorkflowNodeDefinition controlOnly = new("demo.control", 1, inputs: Ports(WorkflowPortDirection.Input, "main"));
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, source, target, controlOnly]);
        WorkflowDocument workflow = Workflow(
            [Start(), new("s", "demo.source", 1), new("s2", "demo.source", 1), new("t", "demo.target", 1), new("c", "demo.control", 1)],
            [
                Connect("start", "main", "s", "main"),
                Connect("start", "main", "s2", "main"),
                Connect("s", "data", "t", "data"),
                Connect("s2", "data", "t", "data"),
                Connect("start", "main", "t", "data"),
                Connect("s", "missing", "c", "main"),
                Connect("c", "main", "s", "main"),
            ]);

        WorkflowAnalysisResult result = new DefaultWorkflowAnalyzer().Analyze(workflow, catalog);

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.IncompatiblePortRoles);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.UnknownSourcePort);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.InvalidPortDirection);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.PortMultiplicityViolation);
        Assert.Equal(WorkflowConnectionRoleCompatibilityStatus.Compatible, result.Connections[1].RoleCompatibilityStatus);
    }

    /// <summary>
    /// Verifies resource-slot kind, capability, required, and optional behavior without live resolution.
    /// </summary>
    [Fact]
    public void AnalyzesResourceSlotsAndCapabilities()
    {
        WorkflowNodeDefinition node = new(
            "demo.resource",
            1,
            resources: new Dictionary<string, WorkflowNodeResourceRequirement>(StringComparer.Ordinal)
            {
                ["browser"] = new("browser", StandardWorkflowResourceKinds.WebBrowser, capabilities: [StandardWorkflowResourceCapabilities.WebFrames]),
                ["optional"] = new("optional", StandardWorkflowResourceKinds.WebPage, required: false),
            });
        WorkflowNodeDefinitionCatalog catalog = new([.. BuiltInWorkflowNodeCatalog.Catalog.Definitions, node]);
        WorkflowDocument workflow = Workflow(
            [
                Start(),
                new("use", "demo.resource", 1, parameters: new JsonObject { ["browser"] = new JsonObject { ["$resource"] = new JsonObject { ["name"] = "browser" } } }),
            ],
            [Connect("start", "main", "use", "main")],
            new Dictionary<string, WorkflowResourceDefinition>(StringComparer.Ordinal)
            {
                ["browser"] = new(StandardWorkflowResourceKinds.WebBrowser, capabilities: [StandardWorkflowResourceCapabilities.WebDownloads]),
            });

        WorkflowAnalysisResult result = new DefaultWorkflowAnalyzer().Analyze(workflow, catalog);

        WorkflowNodeAnalysis resourceNode = result.Nodes.Single(static analysis => analysis.NodeId == "use");
        Assert.Contains(resourceNode.ResourceSlots, static slot => slot.SlotName == "browser");
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowAnalysisCodes.MissingResourceCapability);
        Assert.Contains(resourceNode.ResourceSlots, static slot => slot.SlotName == "optional" && slot.Status == WorkflowResourceRequirementAnalysisStatus.Satisfied);
    }

    /// <summary>
    /// Verifies the analyzer does not mutate inputs and remains deterministic under concurrent use.
    /// </summary>
    [Fact]
    public void IsDeterministicThreadSafeAndDoesNotMutateWorkflowOrCatalog()
    {
        WorkflowDocument workflow = LargeWorkflow(1000, 2000);
        int catalogCount = BuiltInWorkflowNodeCatalog.Catalog.Definitions.Count;
        DefaultWorkflowAnalyzer analyzer = new();

        WorkflowAnalysisResult[] results = Enumerable.Range(0, 4)
            .AsParallel()
            .Select(_ => analyzer.Analyze(workflow, BuiltInWorkflowNodeCatalog.Catalog))
            .ToArray();

        Assert.All(results, result => Assert.Equal(1000, result.Nodes.Count));
        Assert.All(results, result => Assert.Equal(2000, result.Connections.Count));
        Assert.All(results, result => Assert.Equal(results[0].Issues.Select(static issue => issue.Path), result.Issues.Select(static issue => issue.Path)));
        Assert.Equal(catalogCount, BuiltInWorkflowNodeCatalog.Catalog.Definitions.Count);
        Assert.Equal(1000, workflow.Nodes.Count);
    }

    private static WorkflowDocument LargeWorkflow(int nodeCount, int connectionCount)
    {
        List<WorkflowNode> nodes = [Start()];
        for (int index = 1; index < nodeCount; index++)
        {
            nodes.Add(new($"node-{index}", "flow.if", 1, parameters: new JsonObject { ["condition"] = true }));
        }

        List<WorkflowConnection> connections = [];
        for (int index = 0; index < connectionCount; index++)
        {
            int from = index % (nodeCount - 1);
            int to = from + 1;
            string fromNode = from == 0 ? "start" : $"node-{from}";
            connections.Add(Connect(fromNode, from == 0 ? "main" : "true", $"node-{to}", "main"));
        }

        return Workflow(nodes, connections);
    }

    private static WorkflowDocument Workflow(
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection> connections,
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null)
    {
        return new("https://schemas.skeletonkey.dev/workflow/0.1/schema.json", "0.1.0", "workflow", "Workflow", nodes: nodes, connections: connections, resources: resources);
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

    private static IReadOnlyDictionary<string, WorkflowPortDefinition> Ports(WorkflowPortDirection direction, string name, IReadOnlyList<string>? roles = null)
    {
        return new Dictionary<string, WorkflowPortDefinition>(StringComparer.Ordinal)
        {
            [name] = new(name, direction, roles: roles),
        };
    }
}
