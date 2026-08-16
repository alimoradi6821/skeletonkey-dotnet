using SkeletonKey.Analysis;
using SkeletonKey.Catalog;

namespace SkeletonKey.Analysis.Tests;

/// <summary>
/// Covers catalog-aware workflow analysis result contracts.
/// </summary>
public sealed class WorkflowAnalysisContractTests
{
    /// <summary>
    /// Verifies analysis results defensively copy supplied collections.
    /// </summary>
    [Fact]
    public void AnalysisResultDefensivelyCopiesCollections()
    {
        List<WorkflowNodeAnalysis> nodes =
        [
            new("start", "core.start", 1, false, WorkflowNodeCatalogStatus.Known, new WorkflowNodeDefinition("core.start", 1)),
        ];
        List<WorkflowConnectionAnalysis> connections =
        [
            new("start", "main", "end", "main", WorkflowPortCatalogStatus.Known, WorkflowPortCatalogStatus.Known),
        ];
        List<WorkflowAnalysisIssue> issues =
        [
            new(WorkflowAnalysisCodes.UnknownNodeType, WorkflowAnalysisSeverity.Warning, "Unknown.", "/nodes/0", "start", "core.start"),
        ];

        WorkflowAnalysisResult result = new("workflow", "catalog", "1.0.0", nodes, connections, issues);
        nodes.Add(new("end", "core.end", 1, false, WorkflowNodeCatalogStatus.Known));
        connections.Clear();
        issues.Clear();

        Assert.Single(result.Nodes);
        Assert.Single(result.Connections);
        Assert.Single(result.Issues);
        Assert.Equal("catalog", result.CatalogId);
        Assert.Equal("1.0.0", result.CatalogVersion);
    }

    /// <summary>
    /// Verifies analysis readiness is blocked by error severity issues only.
    /// </summary>
    [Fact]
    public void CanPlanExecutionDependsOnErrors()
    {
        WorkflowAnalysisResult warningOnly = new(
            workflowId: "workflow",
            issues:
            [
                new(WorkflowAnalysisCodes.UnknownTargetPort, WorkflowAnalysisSeverity.Warning, "Advisory.", "/connections/0"),
            ]);
        WorkflowAnalysisResult withError = new(
            workflowId: "workflow",
            issues:
            [
                new(WorkflowAnalysisCodes.UnknownNodeType, WorkflowAnalysisSeverity.Error, "Unknown.", "/nodes/0"),
            ]);

        Assert.True(warningOnly.CanPlanExecution);
        Assert.False(withError.CanPlanExecution);
    }

    /// <summary>
    /// Verifies node analysis carries exact catalog status and definition metadata.
    /// </summary>
    [Fact]
    public void NodeAnalysisCarriesCatalogStatusAndDefinition()
    {
        WorkflowNodeDefinition definition = new("core.log", 1, displayName: "Log");

        WorkflowAnalysisIssue issue = new(WorkflowAnalysisCodes.InvalidNodeParameters, WorkflowAnalysisSeverity.Error, "Invalid.", "/nodes/0", "log", "core.log");

        WorkflowNodeAnalysis analysis = new(
            "log",
            "core.log",
            1,
            false,
            WorkflowNodeCatalogStatus.Known,
            definition,
            WorkflowParameterAnalysisStatus.Invalid,
            WorkflowResourceRequirementAnalysisStatus.Satisfied,
            WorkflowCapabilityCompatibilityStatus.Compatible,
            [issue]);

        Assert.Equal("log", analysis.NodeId);
        Assert.Equal(WorkflowNodeCatalogStatus.Known, analysis.CatalogStatus);
        Assert.Same(definition, analysis.Definition);
        Assert.Equal(definition.Key, analysis.DefinitionKey);
        Assert.Equal(WorkflowParameterAnalysisStatus.Invalid, analysis.ParameterStatus);
        Assert.Single(analysis.Issues);
    }

    /// <summary>
    /// Verifies connection analysis preserves endpoint identity and richer status metadata.
    /// </summary>
    [Fact]
    public void ConnectionAnalysisPreservesEndpointAndStatusMetadata()
    {
        WorkflowAnalysisIssue issue = new(WorkflowAnalysisCodes.InvalidPortDirection, WorkflowAnalysisSeverity.Error, "Invalid.", "/connections/0");

        WorkflowConnectionAnalysis analysis = new(
            "from",
            "result",
            "to",
            "main",
            WorkflowPortCatalogStatus.Known,
            WorkflowPortCatalogStatus.WrongDirection,
            0,
            WorkflowDynamicPortAnalysisStatus.Resolved,
            WorkflowConnectionRoleCompatibilityStatus.InvalidDirection,
            [issue]);

        Assert.Equal("from", analysis.FromNode);
        Assert.Equal("main", analysis.ToPort);
        Assert.Equal(0, analysis.ConnectionIndex);
        Assert.Equal(WorkflowDynamicPortAnalysisStatus.Resolved, analysis.DynamicPortStatus);
        Assert.Equal(WorkflowConnectionRoleCompatibilityStatus.InvalidDirection, analysis.RoleCompatibilityStatus);
        Assert.Single(analysis.Issues);
    }
}
