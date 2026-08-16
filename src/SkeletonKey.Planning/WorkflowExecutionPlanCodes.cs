namespace SkeletonKey.Planning;

/// <summary>
/// Defines stable workflow execution planning issue codes.
/// </summary>
public static class WorkflowExecutionPlanCodes
{
    /// <summary>
    /// Planning was blocked because semantic validation has errors.
    /// </summary>
    public const string ValidationErrors = "SKP1001";

    /// <summary>
    /// Planning was blocked because catalog-aware analysis has errors.
    /// </summary>
    public const string AnalysisErrors = "SKP1002";

    /// <summary>
    /// Planning was blocked because a required node definition is missing.
    /// </summary>
    public const string MissingNodeDefinition = "SKP1005";

    /// <summary>
    /// Planning was blocked because a dynamic port could not be resolved.
    /// </summary>
    public const string UnresolvedDynamicPort = "SKP1006";

    /// <summary>
    /// Planning was blocked because a dependency declaration is invalid.
    /// </summary>
    public const string InvalidDependency = "SKP1007";

    /// <summary>
    /// Planning was blocked because a dependency cycle prevents the selected plan shape.
    /// </summary>
    public const string DependencyCycle = "SKP1008";

    /// <summary>
    /// Planning was blocked because a loop structure is invalid.
    /// </summary>
    public const string InvalidLoopStructure = "SKP1009";

    /// <summary>
    /// Planning was blocked because no entry step could be selected.
    /// </summary>
    public const string MissingEntryStep = "SKP1010";

    /// <summary>
    /// Planning was blocked because no terminal path could be proven.
    /// </summary>
    public const string MissingTerminalPath = "SKP1011";

    /// <summary>
    /// Planning was blocked because a resource requirement is unsatisfied.
    /// </summary>
    public const string UnsatisfiedResourceRequirement = "SKP1012";

    /// <summary>
    /// Planning was blocked because an execution characteristic is unsupported.
    /// </summary>
    public const string UnsupportedExecutionCharacteristic = "SKP1013";

    /// <summary>
    /// Planning was blocked because an invocation boundary is invalid.
    /// </summary>
    public const string InvalidInvocationBoundary = "SKP1014";

    /// <summary>
    /// Planning was blocked because the analysis result does not describe the supplied workflow.
    /// </summary>
    public const string AnalysisWorkflowMismatch = "SKP1015";

    /// <summary>
    /// Planning was blocked because the plan would exceed configured size limits.
    /// </summary>
    public const string PlanLimitExceeded = "SKP1016";

    /// <summary>
    /// Planning was blocked because an expression or binding dependency references an unresolved node or port.
    /// </summary>
    public const string UnresolvedDataDependency = "SKP1017";

    /// <summary>
    /// Planning was blocked because the workflow graph cannot be topologically ordered for the selected strategy.
    /// </summary>
    public const string GraphOrderingUnavailable = "SKP1003";

    /// <summary>
    /// Planning was blocked because required resource scheduling metadata is unavailable.
    /// </summary>
    public const string ResourceSchedulingUnavailable = "SKP1004";
}
