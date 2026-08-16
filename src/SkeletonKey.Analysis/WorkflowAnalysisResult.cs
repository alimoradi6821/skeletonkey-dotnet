namespace SkeletonKey.Analysis;

/// <summary>
/// Represents the immutable result of catalog-aware workflow analysis.
/// </summary>
public sealed class WorkflowAnalysisResult
{
    private static readonly IReadOnlyList<WorkflowNodeAnalysis> _emptyNodes = Array.AsReadOnly(Array.Empty<WorkflowNodeAnalysis>());
    private static readonly IReadOnlyList<WorkflowConnectionAnalysis> _emptyConnections = Array.AsReadOnly(Array.Empty<WorkflowConnectionAnalysis>());
    private static readonly IReadOnlyList<WorkflowAnalysisIssue> _emptyIssues = Array.AsReadOnly(Array.Empty<WorkflowAnalysisIssue>());

    /// <summary>
    /// Initializes an analysis result.
    /// </summary>
    /// <param name="workflowId">The analyzed workflow identifier.</param>
    /// <param name="catalogId">Optional catalog identifier used for analysis.</param>
    /// <param name="catalogVersion">Optional exact catalog version used for analysis.</param>
    /// <param name="nodes">Catalog analysis for workflow nodes.</param>
    /// <param name="connections">Catalog analysis for workflow connections.</param>
    /// <param name="issues">Analysis issues in deterministic order.</param>
    /// <param name="workflowSpecVersion">Optional workflow specification version used during analysis.</param>
    public WorkflowAnalysisResult(
        string workflowId,
        string? catalogId = null,
        string? catalogVersion = null,
        IReadOnlyList<WorkflowNodeAnalysis>? nodes = null,
        IReadOnlyList<WorkflowConnectionAnalysis>? connections = null,
        IReadOnlyList<WorkflowAnalysisIssue>? issues = null,
        string? workflowSpecVersion = null)
    {
        WorkflowId = workflowId;
        WorkflowSpecVersion = workflowSpecVersion;
        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        Nodes = nodes is null ? _emptyNodes : Array.AsReadOnly([.. nodes]);
        Connections = connections is null ? _emptyConnections : Array.AsReadOnly([.. connections]);
        Issues = issues is null ? _emptyIssues : Array.AsReadOnly([.. issues]);
    }

    /// <summary>
    /// Gets the analyzed workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the optional workflow specification version used during analysis.
    /// </summary>
    public string? WorkflowSpecVersion { get; }

    /// <summary>
    /// Gets the optional catalog identifier used for analysis.
    /// </summary>
    public string? CatalogId { get; }

    /// <summary>
    /// Gets the optional exact catalog version used for analysis.
    /// </summary>
    public string? CatalogVersion { get; }

    /// <summary>
    /// Gets catalog analysis for workflow nodes.
    /// </summary>
    public IReadOnlyList<WorkflowNodeAnalysis> Nodes { get; }

    /// <summary>
    /// Gets catalog analysis for workflow connections.
    /// </summary>
    public IReadOnlyList<WorkflowConnectionAnalysis> Connections { get; }

    /// <summary>
    /// Gets analysis issues in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowAnalysisIssue> Issues { get; }

    /// <summary>
    /// Gets error issues.
    /// </summary>
    public IEnumerable<WorkflowAnalysisIssue> Errors => Issues.Where(issue => issue.Severity == WorkflowAnalysisSeverity.Error);

    /// <summary>
    /// Gets warning issues.
    /// </summary>
    public IEnumerable<WorkflowAnalysisIssue> Warnings => Issues.Where(issue => issue.Severity == WorkflowAnalysisSeverity.Warning);

    /// <summary>
    /// Gets a value indicating whether the analysis found no planning-blocking issues.
    /// </summary>
    public bool CanPlanExecution => !Errors.Any();
}
