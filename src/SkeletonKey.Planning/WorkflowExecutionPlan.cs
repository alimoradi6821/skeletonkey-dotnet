namespace SkeletonKey.Planning;

/// <summary>
/// Represents a host-neutral execution plan contract for a workflow.
/// </summary>
public sealed class WorkflowExecutionPlan
{
    private static readonly IReadOnlyList<WorkflowExecutionPlanStep> _emptySteps = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanStep>());
    private static readonly IReadOnlyList<WorkflowExecutionPlanDependency> _emptyDependencies = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanDependency>());
    private static readonly IReadOnlyList<WorkflowExecutionPlanResource> _emptyResources = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanResource>());
    private static readonly IReadOnlyDictionary<string, string> _emptyNodeStepMap = new Dictionary<string, string>();
    private static readonly IReadOnlyList<string> _emptyStepIds = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    /// Initializes an execution plan contract.
    /// </summary>
    /// <param name="planId">The stable plan identifier assigned by a planner.</param>
    /// <param name="workflowId">The planned workflow identifier.</param>
    /// <param name="workflowSpecVersion">The workflow specification version used for planning.</param>
    /// <param name="catalogId">Optional catalog identifier used for planning.</param>
    /// <param name="catalogVersion">Optional exact catalog version used for planning.</param>
    /// <param name="steps">Ordered plan steps.</param>
    /// <param name="resources">Workflow resource declarations referenced by the plan.</param>
    /// <param name="nodeStepMap">Node-to-step mapping.</param>
    /// <param name="entryStepIds">Entry step IDs.</param>
    /// <param name="terminalStepIds">Terminal step IDs.</param>
    /// <param name="dependencies">All explicit plan dependencies in deterministic order.</param>
    public WorkflowExecutionPlan(
        string planId,
        string workflowId,
        string workflowSpecVersion,
        string? catalogId = null,
        string? catalogVersion = null,
        IReadOnlyList<WorkflowExecutionPlanStep>? steps = null,
        IReadOnlyList<WorkflowExecutionPlanResource>? resources = null,
        IReadOnlyDictionary<string, string>? nodeStepMap = null,
        IReadOnlyList<string>? entryStepIds = null,
        IReadOnlyList<string>? terminalStepIds = null,
        IReadOnlyList<WorkflowExecutionPlanDependency>? dependencies = null)
    {
        PlanId = planId;
        WorkflowId = workflowId;
        WorkflowSpecVersion = workflowSpecVersion;
        CatalogId = catalogId;
        CatalogVersion = catalogVersion;
        Steps = steps is null ? _emptySteps : Array.AsReadOnly([.. steps]);
        Resources = resources is null ? _emptyResources : Array.AsReadOnly([.. resources]);
        Dependencies = dependencies is null ? _emptyDependencies : Array.AsReadOnly([.. dependencies]);
        NodeStepMap = nodeStepMap is null ? _emptyNodeStepMap : new Dictionary<string, string>(nodeStepMap, StringComparer.Ordinal);
        EntryStepIds = entryStepIds is null ? _emptyStepIds : Array.AsReadOnly([.. entryStepIds]);
        TerminalStepIds = terminalStepIds is null ? _emptyStepIds : Array.AsReadOnly([.. terminalStepIds]);
    }

    /// <summary>
    /// Gets the stable plan identifier assigned by a planner.
    /// </summary>
    public string PlanId { get; }

    /// <summary>
    /// Gets the planned workflow identifier.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the workflow specification version used for planning.
    /// </summary>
    public string WorkflowSpecVersion { get; }

    /// <summary>
    /// Gets the optional catalog identifier used for planning.
    /// </summary>
    public string? CatalogId { get; }

    /// <summary>
    /// Gets the optional exact catalog version used for planning.
    /// </summary>
    public string? CatalogVersion { get; }

    /// <summary>
    /// Gets ordered plan steps.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanStep> Steps { get; }

    /// <summary>
    /// Gets workflow resource declarations referenced by the plan.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanResource> Resources { get; }

    /// <summary>
    /// Gets all explicit plan dependencies in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanDependency> Dependencies { get; }

    /// <summary>
    /// Gets node-to-step mapping keyed by workflow node ID.
    /// </summary>
    public IReadOnlyDictionary<string, string> NodeStepMap { get; }

    /// <summary>
    /// Gets entry step IDs.
    /// </summary>
    public IReadOnlyList<string> EntryStepIds { get; }

    /// <summary>
    /// Gets terminal step IDs.
    /// </summary>
    public IReadOnlyList<string> TerminalStepIds { get; }
}
