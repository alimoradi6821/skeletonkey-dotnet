namespace SkeletonKey.Planning;

/// <summary>
/// Describes one node scheduled in an execution plan contract.
/// </summary>
public sealed class WorkflowExecutionPlanStep
{
    private static readonly IReadOnlyList<WorkflowExecutionPlanDependency> _emptyDependencies = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanDependency>());
    private static readonly IReadOnlyList<WorkflowExecutionPlanResourceUse> _emptyResources = Array.AsReadOnly(Array.Empty<WorkflowExecutionPlanResourceUse>());
    private static readonly IReadOnlyList<NodeLocatorUse> _emptyLocators = Array.AsReadOnly(Array.Empty<NodeLocatorUse>());

    /// <summary>
    /// Initializes an execution plan step.
    /// </summary>
    /// <param name="stepId">The stable plan step identifier.</param>
    /// <param name="nodeId">The workflow node identifier.</param>
    /// <param name="nodeType">The workflow node type identifier.</param>
    /// <param name="typeVersion">The workflow node type version.</param>
    /// <param name="dependsOn">Plan dependencies that must complete before this step runs.</param>
    /// <param name="resources">Planned resource uses for this step.</param>
    /// <param name="kind">The high-level planned step kind.</param>
    /// <param name="maySuspend">Whether this step may suspend in a future runtime.</param>
    /// <param name="terminal">Whether this step is terminal for its active flow path.</param>
    /// <param name="controlBoundary">Optional control-flow boundary metadata.</param>
    /// <param name="invocationBoundary">Optional invocation boundary metadata.</param>
    /// <param name="loopBoundary">Optional loop boundary metadata.</param>
    /// <param name="locators">Planned locator uses for this step.</param>
    public WorkflowExecutionPlanStep(
        string stepId,
        string nodeId,
        string nodeType,
        int typeVersion,
        IReadOnlyList<WorkflowExecutionPlanDependency>? dependsOn = null,
        IReadOnlyList<WorkflowExecutionPlanResourceUse>? resources = null,
        WorkflowExecutionPlanStepKind kind = WorkflowExecutionPlanStepKind.Action,
        bool maySuspend = false,
        bool terminal = false,
        WorkflowExecutionPlanBoundary? controlBoundary = null,
        WorkflowExecutionPlanBoundary? invocationBoundary = null,
        WorkflowExecutionPlanBoundary? loopBoundary = null,
        IReadOnlyList<NodeLocatorUse>? locators = null)
    {
        StepId = stepId;
        NodeId = nodeId;
        NodeType = nodeType;
        TypeVersion = typeVersion;
        DefinitionKey = new(nodeType, typeVersion);
        DependsOn = dependsOn is null ? _emptyDependencies : Array.AsReadOnly([.. dependsOn]);
        Resources = resources is null ? _emptyResources : Array.AsReadOnly([.. resources]);
        Locators = locators is null ? _emptyLocators : Array.AsReadOnly([.. locators]);
        Kind = kind;
        MaySuspend = maySuspend;
        Terminal = terminal;
        ControlBoundary = controlBoundary;
        InvocationBoundary = invocationBoundary;
        LoopBoundary = loopBoundary;
    }

    /// <summary>
    /// Gets the stable plan step identifier.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    /// Gets the workflow node identifier.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the workflow node type identifier.
    /// </summary>
    public string NodeType { get; }

    /// <summary>
    /// Gets the workflow node type version.
    /// </summary>
    public int TypeVersion { get; }

    /// <summary>
    /// Gets the resolved node definition key.
    /// </summary>
    public Catalog.WorkflowNodeDefinitionKey DefinitionKey { get; }

    /// <summary>
    /// Gets plan dependencies that must complete before this step runs.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanDependency> DependsOn { get; }

    /// <summary>
    /// Gets planned resource uses for this step.
    /// </summary>
    public IReadOnlyList<WorkflowExecutionPlanResourceUse> Resources { get; }

    /// <summary>
    /// Gets planned locator uses for this step.
    /// </summary>
    public IReadOnlyList<NodeLocatorUse> Locators { get; }

    /// <summary>
    /// Gets the high-level planned step kind.
    /// </summary>
    public WorkflowExecutionPlanStepKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether this step may suspend in a future runtime.
    /// </summary>
    public bool MaySuspend { get; }

    /// <summary>
    /// Gets a value indicating whether this step is terminal for its active flow path.
    /// </summary>
    public bool Terminal { get; }

    /// <summary>
    /// Gets optional control-flow boundary metadata.
    /// </summary>
    public WorkflowExecutionPlanBoundary? ControlBoundary { get; }

    /// <summary>
    /// Gets optional invocation boundary metadata.
    /// </summary>
    public WorkflowExecutionPlanBoundary? InvocationBoundary { get; }

    /// <summary>
    /// Gets optional loop boundary metadata.
    /// </summary>
    public WorkflowExecutionPlanBoundary? LoopBoundary { get; }
}
