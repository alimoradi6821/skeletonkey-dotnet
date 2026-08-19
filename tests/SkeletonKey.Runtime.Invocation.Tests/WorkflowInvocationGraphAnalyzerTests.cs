using System.Text.Json.Nodes;
using SkeletonKey.Runtime.Invocation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation.Tests;

/// <summary>Covers deterministic cross-workflow invocation graph analysis.</summary>
public sealed class WorkflowInvocationGraphAnalyzerTests
{
    /// <summary>Verifies a resolved child with compatible inputs and stream mappings is accepted.</summary>
    [Fact]
    public async Task AcceptsCompatibleReachableInvocationGraph()
    {
        WorkflowDocument child = Workflow(
            "child",
            [Start()],
            inputs: new Dictionary<string, WorkflowInputDefinition>(StringComparer.Ordinal)
            {
                ["name"] = new(WorkflowInputType.String, required: true),
            },
            outputs: new Dictionary<string, WorkflowOutputDefinition>(StringComparer.Ordinal)
            {
                ["records"] = new(WorkflowOutputMode.Stream, channel: "child-records"),
            });
        WorkflowDocument root = Workflow(
            "root",
            [
                Start(),
                Invoke("invoke", "child", inputs: new JsonObject { ["name"] = "Ada" }, streams: new JsonObject
                {
                    ["mode"] = "map",
                    ["mappings"] = new JsonObject { ["child-records"] = "root-records" },
                }),
            ],
            [Connect("start", "main", "invoke", "main")]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, ImmutableWorkflowRepository.FromDocuments(child));

        Assert.True(result.IsValid);
        WorkflowInvocationDependency dependency = Assert.Single(result.Dependencies);
        Assert.Equal("root", dependency.ParentWorkflowId);
        Assert.Equal("child", dependency.ChildWorkflowId);
        Assert.Equal(1, dependency.Depth);
    }

    /// <summary>Verifies child input names, required values, and static types are checked together.</summary>
    [Fact]
    public async Task ReportsChildInputContractErrors()
    {
        WorkflowDocument child = Workflow(
            "child",
            [Start()],
            inputs: new Dictionary<string, WorkflowInputDefinition>(StringComparer.Ordinal)
            {
                ["requiredName"] = new(WorkflowInputType.String, required: true),
                ["count"] = new(WorkflowInputType.Integer),
            });
        WorkflowDocument root = Workflow(
            "root",
            [Start(), Invoke("invoke", "child", new JsonObject { ["count"] = "wrong", ["extra"] = true })],
            [Connect("start", "main", "invoke", "main")]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, ImmutableWorkflowRepository.FromDocuments(child));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowInvocationAnalysisCodes.RequiredChildInputMissing);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowInvocationAnalysisCodes.UnknownChildInput);
        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowInvocationAnalysisCodes.ChildInputTypeMismatch);
    }

    /// <summary>Verifies dynamic workflow values defer child input type checking until materialization.</summary>
    [Fact]
    public async Task DefersDynamicChildInputTypeChecking()
    {
        WorkflowDocument child = Workflow(
            "child",
            [Start()],
            inputs: new Dictionary<string, WorkflowInputDefinition>(StringComparer.Ordinal)
            {
                ["count"] = new(WorkflowInputType.Integer, required: true),
            });
        JsonObject binding = new() { ["$binding"] = new JsonObject { ["source"] = "inputs.count" } };
        WorkflowDocument root = Workflow(
            "root",
            [Start(), Invoke("invoke", "child", new JsonObject { ["count"] = binding })],
            [Connect("start", "main", "invoke", "main")]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, ImmutableWorkflowRepository.FromDocuments(child));

        Assert.True(result.IsValid);
    }

    /// <summary>Verifies missing reachable dependencies fail while disconnected invocation nodes remain ignored.</summary>
    [Fact]
    public async Task ReportsOnlyReachableMissingDependencies()
    {
        WorkflowDocument root = Workflow(
            "root",
            [Start(), Invoke("reachable", "missing"), Invoke("disconnected", "also-missing")],
            [Connect("start", "main", "reachable", "main")]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(
            root,
            new ImmutableWorkflowRepository(new Dictionary<string, WorkflowDocument>(StringComparer.Ordinal)));

        WorkflowInvocationAnalysisIssue issue = Assert.Single(result.Issues);
        Assert.Equal(WorkflowInvocationAnalysisCodes.WorkflowNotFound, issue.Code);
        Assert.Equal("reachable", issue.NodeId);
    }

    /// <summary>Verifies direct and indirect recursion is rejected before runtime execution.</summary>
    [Fact]
    public async Task RejectsInvocationCycles()
    {
        WorkflowDocument root = Workflow("root", [Start(), Invoke("to-child", "child")], [Connect("start", "main", "to-child", "main")]);
        WorkflowDocument child = Workflow("child", [Start(), Invoke("to-root", "root")], [Connect("start", "main", "to-root", "main")]);
        var repository = ImmutableWorkflowRepository.FromDocuments(root, child);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, repository);

        WorkflowInvocationAnalysisIssue issue = Assert.Single(result.Issues);
        Assert.Equal(WorkflowInvocationAnalysisCodes.InvocationCycle, issue.Code);
        Assert.Equal("to-root", issue.NodeId);
    }

    /// <summary>Verifies dependency depth is bounded independently of runtime activation limits.</summary>
    [Fact]
    public async Task RejectsInvocationDepthBeyondConfiguredLimit()
    {
        WorkflowDocument root = Workflow("root", [Start(), Invoke("to-child", "child")], [Connect("start", "main", "to-child", "main")]);
        WorkflowDocument child = Workflow("child", [Start(), Invoke("to-grand", "grand")], [Connect("start", "main", "to-grand", "main")]);
        WorkflowDocument grand = Workflow("grand", [Start()]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(
            root,
            ImmutableWorkflowRepository.FromDocuments(child, grand),
            new WorkflowInvocationAnalysisOptions(maximumDepth: 1));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowInvocationAnalysisCodes.InvocationDepthExceeded && issue.NodeId == "to-grand");
    }

    /// <summary>Verifies an exact version reference never falls back to an unversioned registration.</summary>
    [Fact]
    public async Task ExactVersionLookupDoesNotFallBackToUnversionedWorkflow()
    {
        WorkflowDocument child = Workflow("child", [Start()]);
        var repository = ImmutableWorkflowRepository.FromDocuments(child);

        WorkflowRepositoryLookupResult result = await repository.LookupAsync(new WorkflowReference("child", "1.0.0"));

        Assert.False(result.Found);
    }

    /// <summary>Verifies mapped child stream sources must exist in the resolved child contract.</summary>
    [Fact]
    public async Task RejectsUnknownMappedChildStreamChannel()
    {
        WorkflowDocument child = Workflow("child", [Start()]);
        WorkflowDocument root = Workflow(
            "root",
            [
                Start(),
                Invoke("invoke", "child", streams: new JsonObject
                {
                    ["mode"] = "map",
                    ["mappings"] = new JsonObject { ["missing-records"] = "root-records" },
                }),
            ],
            [Connect("start", "main", "invoke", "main")]);

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, ImmutableWorkflowRepository.FromDocuments(child));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowInvocationAnalysisCodes.UnknownChildStreamChannel);
    }

    /// <summary>Verifies a repository cannot substitute a different workflow identity.</summary>
    [Fact]
    public async Task RejectsResolvedWorkflowIdentityMismatch()
    {
        WorkflowDocument root = Workflow(
            "root",
            [Start(), Invoke("invoke", "expected")],
            [Connect("start", "main", "invoke", "main")]);
        IWorkflowRepository repository = new DelegateWorkflowRepository(static (_, _) =>
            ValueTask.FromResult(WorkflowRepositoryLookupResult.Success(Workflow("different", [Start()]))));

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, repository);

        WorkflowInvocationAnalysisIssue issue = Assert.Single(result.Issues);
        Assert.Equal(WorkflowInvocationAnalysisCodes.WorkflowIdentityMismatch, issue.Code);
    }

    /// <summary>Verifies repository exceptions become stable host-neutral analysis issues.</summary>
    [Fact]
    public async Task NormalizesWorkflowRepositoryFailure()
    {
        WorkflowDocument root = Workflow(
            "root",
            [Start(), Invoke("invoke", "child")],
            [Connect("start", "main", "invoke", "main")]);
        IWorkflowRepository repository = new DelegateWorkflowRepository(static (_, _) => throw new InvalidOperationException("secret repository detail"));

        WorkflowInvocationAnalysisResult result = await new WorkflowInvocationGraphAnalyzer().AnalyzeAsync(root, repository);

        WorkflowInvocationAnalysisIssue issue = Assert.Single(result.Issues);
        Assert.Equal(WorkflowInvocationAnalysisCodes.RepositoryFailure, issue.Code);
        Assert.DoesNotContain("secret", issue.Message, StringComparison.Ordinal);
    }

    private static WorkflowDocument Workflow(
        string id,
        IReadOnlyList<WorkflowNode> nodes,
        IReadOnlyList<WorkflowConnection>? connections = null,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyDictionary<string, WorkflowOutputDefinition>? outputs = null)
    {
        return new WorkflowDocument(id: id, name: id, inputs: inputs, nodes: nodes, connections: connections, outputs: outputs);
    }

    private static WorkflowNode Start()
    {
        return new WorkflowNode("start", "core.start", 1);
    }

    private static WorkflowNode Invoke(string nodeId, string workflowId, JsonObject? inputs = null, JsonObject? streams = null)
    {
        JsonObject parameters = new()
        {
            ["workflow"] = new JsonObject { ["id"] = workflowId },
        };
        if (inputs is not null)
        {
            parameters["inputs"] = inputs;
        }

        if (streams is not null)
        {
            parameters["streams"] = streams;
        }

        return new WorkflowNode(nodeId, "workflow.invoke", 1, parameters: parameters);
    }

    private static WorkflowConnection Connect(string fromNode, string fromPort, string toNode, string toPort)
    {
        return new WorkflowConnection(new WorkflowEndpoint(fromNode, fromPort), new WorkflowEndpoint(toNode, toPort));
    }

    private sealed class DelegateWorkflowRepository(
        Func<WorkflowReference, CancellationToken, ValueTask<WorkflowRepositoryLookupResult>> lookup) : IWorkflowRepository
    {
        public ValueTask<WorkflowRepositoryLookupResult> LookupAsync(WorkflowReference reference, CancellationToken cancellationToken = default)
        {
            return lookup(reference, cancellationToken);
        }
    }
}
