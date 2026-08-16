using SkeletonKey.Validation.Tests.Support;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;

namespace SkeletonKey.Validation.Tests.Rules;

/// <summary>
/// Covers workflow output semantic validation.
/// </summary>
public sealed class OutputValidationTests
{
    /// <summary>
    /// Verifies valid single outputs are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidSingleOutput()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "main")),
            }));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies valid collection outputs are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidCollectionOutput()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["items"] = new WorkflowOutputDefinition(WorkflowOutputMode.Collection, new WorkflowEndpoint("log", "items")),
            }));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies valid stream outputs are accepted.
    /// </summary>
    [Fact]
    public void AcceptsValidStreamOutput()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["events"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "records.done"),
            }));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies invalid output names are rejected.
    /// </summary>
    [Fact]
    public void RejectsInvalidOutputName()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["bad/name"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "main")),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidWorkflowOutputName && issue.Path == "/outputs/bad~1name");
    }

    /// <summary>
    /// Verifies value outputs require a source endpoint.
    /// </summary>
    [Fact]
    public void RejectsValueOutputWithoutSourceEndpoint()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.ValueOutputRequiresSourceEndpoint && issue.Path == "/outputs/result/from");
    }

    /// <summary>
    /// Verifies stream outputs require a channel.
    /// </summary>
    [Fact]
    public void RejectsStreamOutputWithoutChannel()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["events"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.StreamOutputRequiresChannel && issue.Path == "/outputs/events/channel");
    }

    /// <summary>
    /// Verifies value outputs must not declare channels.
    /// </summary>
    [Fact]
    public void RejectsValueOutputWithChannel()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "main"), channel: "records"),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.OutputIncompatibleProperties && issue.Path == "/outputs/result");
    }

    /// <summary>
    /// Verifies stream outputs must not declare source endpoints.
    /// </summary>
    [Fact]
    public void RejectsStreamOutputWithSourceEndpoint()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["events"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, new WorkflowEndpoint("log", "main"), channel: "records"),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.OutputIncompatibleProperties && issue.Path == "/outputs/events");
    }

    /// <summary>
    /// Verifies output source nodes must exist.
    /// </summary>
    [Fact]
    public void RejectsUnknownOutputSourceNode()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("missing", "main")),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.OutputUnknownSourceNode && issue.Path == "/outputs/result/from/node");
    }

    /// <summary>
    /// Verifies output source ports must be valid port names.
    /// </summary>
    [Fact]
    public void RejectsInvalidOutputSourcePort()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "bad port")),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidOutputSourcePort && issue.Path == "/outputs/result/from/port");
    }

    /// <summary>
    /// Verifies stream output channels must match the channel naming pattern.
    /// </summary>
    [Fact]
    public void RejectsInvalidOutputChannel()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["events"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "Records"),
            }));

        Assert.Contains(result.Issues, static issue => issue.Code == WorkflowValidationCodes.InvalidOutputChannelName && issue.Path == "/outputs/events/channel");
    }

    /// <summary>
    /// Verifies output validation reports diagnostics deterministically.
    /// </summary>
    [Fact]
    public void ReportsOutputDiagnosticsDeterministically()
    {
        WorkflowValidationResult result = ValidationTestData.Validate(CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["bad/name"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single),
                ["events"] = new WorkflowOutputDefinition(WorkflowOutputMode.Stream, channel: "Records"),
            }));

        Assert.Equal(
            [
                WorkflowValidationCodes.InvalidWorkflowOutputName,
                WorkflowValidationCodes.ValueOutputRequiresSourceEndpoint,
                WorkflowValidationCodes.InvalidOutputChannelName,
            ],
            [.. result.Issues.Where(static issue => issue.Code.StartsWith("SKW28", StringComparison.Ordinal)).Select(static issue => issue.Code)]);
    }

    /// <summary>
    /// Verifies validation does not mutate output declarations.
    /// </summary>
    [Fact]
    public void ValidationDoesNotMutateOutputDeclarations()
    {
        WorkflowDocument workflow = CreateWorkflowWithOutputs(
            new Dictionary<string, WorkflowOutputDefinition>
            {
                ["result"] = new WorkflowOutputDefinition(WorkflowOutputMode.Single, new WorkflowEndpoint("log", "main")),
            });

        _ = ValidationTestData.Validate(workflow);

        Assert.Single(workflow.Outputs);
        Assert.Equal(new WorkflowEndpoint("log", "main"), workflow.Outputs["result"].From);
    }

    private static WorkflowDocument CreateWorkflowWithOutputs(IReadOnlyDictionary<string, WorkflowOutputDefinition> outputs)
    {
        WorkflowNode[] nodes =
        [
            new("start", "core.start", 1),
            new("log", "core.log", 1),
        ];
        WorkflowConnection[] connections =
        [
            new(new WorkflowEndpoint("start", "main"), new WorkflowEndpoint("log", "main")),
        ];

        return ValidationTestData.CreateValidWorkflow(nodes: nodes, connections: connections, outputs: outputs);
    }
}
