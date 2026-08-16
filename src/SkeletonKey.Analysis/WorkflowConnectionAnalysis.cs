namespace SkeletonKey.Analysis;

/// <summary>
/// Represents catalog analysis for one workflow connection.
/// </summary>
public sealed class WorkflowConnectionAnalysis
{
    private static readonly IReadOnlyList<WorkflowAnalysisIssue> _emptyIssues = Array.AsReadOnly(Array.Empty<WorkflowAnalysisIssue>());

    /// <summary>
    /// Initializes connection analysis.
    /// </summary>
    /// <param name="fromNode">The source node identifier.</param>
    /// <param name="fromPort">The source port name.</param>
    /// <param name="toNode">The target node identifier.</param>
    /// <param name="toPort">The target port name.</param>
    /// <param name="sourcePortStatus">Catalog status for the source endpoint.</param>
    /// <param name="targetPortStatus">Catalog status for the target endpoint.</param>
    /// <param name="connectionIndex">The workflow connection index, when known.</param>
    /// <param name="dynamicPortStatus">Dynamic port resolution status.</param>
    /// <param name="roleCompatibilityStatus">Connection role compatibility status.</param>
    /// <param name="issues">Connection-specific issues in deterministic order.</param>
    /// <param name="sourcePort">Resolved effective source port, when available.</param>
    /// <param name="targetPort">Resolved effective target port, when available.</param>
    public WorkflowConnectionAnalysis(
        string fromNode,
        string fromPort,
        string toNode,
        string toPort,
        WorkflowPortCatalogStatus sourcePortStatus,
        WorkflowPortCatalogStatus targetPortStatus,
        int? connectionIndex = null,
        WorkflowDynamicPortAnalysisStatus dynamicPortStatus = WorkflowDynamicPortAnalysisStatus.NotAnalyzed,
        WorkflowConnectionRoleCompatibilityStatus roleCompatibilityStatus = WorkflowConnectionRoleCompatibilityStatus.NotAnalyzed,
        IReadOnlyList<WorkflowAnalysisIssue>? issues = null,
        WorkflowEffectivePort? sourcePort = null,
        WorkflowEffectivePort? targetPort = null)
    {
        FromNode = fromNode;
        FromPort = fromPort;
        ToNode = toNode;
        ToPort = toPort;
        SourcePortStatus = sourcePortStatus;
        TargetPortStatus = targetPortStatus;
        ConnectionIndex = connectionIndex;
        DynamicPortStatus = dynamicPortStatus;
        RoleCompatibilityStatus = roleCompatibilityStatus;
        Issues = issues is null ? _emptyIssues : Array.AsReadOnly([.. issues]);
        SourcePort = sourcePort;
        TargetPort = targetPort;
    }

    /// <summary>
    /// Gets the source node identifier.
    /// </summary>
    public string FromNode { get; }

    /// <summary>
    /// Gets the source port name.
    /// </summary>
    public string FromPort { get; }

    /// <summary>
    /// Gets the target node identifier.
    /// </summary>
    public string ToNode { get; }

    /// <summary>
    /// Gets the target port name.
    /// </summary>
    public string ToPort { get; }

    /// <summary>
    /// Gets catalog status for the source endpoint.
    /// </summary>
    public WorkflowPortCatalogStatus SourcePortStatus { get; }

    /// <summary>
    /// Gets catalog status for the target endpoint.
    /// </summary>
    public WorkflowPortCatalogStatus TargetPortStatus { get; }

    /// <summary>
    /// Gets the workflow connection index, when known.
    /// </summary>
    public int? ConnectionIndex { get; }

    /// <summary>
    /// Gets dynamic port resolution status.
    /// </summary>
    public WorkflowDynamicPortAnalysisStatus DynamicPortStatus { get; }

    /// <summary>
    /// Gets connection role compatibility status.
    /// </summary>
    public WorkflowConnectionRoleCompatibilityStatus RoleCompatibilityStatus { get; }

    /// <summary>
    /// Gets the resolved effective source port, when available.
    /// </summary>
    public WorkflowEffectivePort? SourcePort { get; }

    /// <summary>
    /// Gets the resolved effective target port, when available.
    /// </summary>
    public WorkflowEffectivePort? TargetPort { get; }

    /// <summary>
    /// Gets connection-specific issues in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowAnalysisIssue> Issues { get; }
}
