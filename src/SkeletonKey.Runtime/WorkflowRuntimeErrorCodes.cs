namespace SkeletonKey.Runtime;

/// <summary>
/// Defines stable runtime error codes for workflow execution failures.
/// </summary>
public static class WorkflowRuntimeErrorCodes
{
    /// <summary>Semantic validation failed.</summary>
    public const string SemanticValidationFailed = "SKR1001";

    /// <summary>Catalog-aware analysis failed.</summary>
    public const string CatalogAnalysisFailed = "SKR1002";

    /// <summary>Execution planning failed.</summary>
    public const string PlanningFailed = "SKR1003";

    /// <summary>A required exact node handler was missing.</summary>
    public const string MissingNodeHandler = "SKR1004";

    /// <summary>A resolved handler declared a different exact node definition.</summary>
    public const string HandlerIdentityMismatch = "SKR1005";

    /// <summary>Node parameter materialization failed.</summary>
    public const string ParameterMaterializationFailed = "SKR1006";

    /// <summary>A handler threw an unexpected exception.</summary>
    public const string HandlerUnexpectedException = "SKR1007";

    /// <summary>A handler returned an invalid control output.</summary>
    public const string InvalidHandlerControlOutput = "SKR1008";

    /// <summary>A handler returned an invalid data output.</summary>
    public const string InvalidHandlerDataOutput = "SKR1009";

    /// <summary>A required dependency was unavailable.</summary>
    public const string RequiredDependencyUnavailable = "SKR1010";

    /// <summary>A deterministic execution limit was exceeded.</summary>
    public const string ExecutionLimitExceeded = "SKR1011";

    /// <summary>Execution was cancelled.</summary>
    public const string ExecutionCancelled = "SKR1012";

    /// <summary>A reachable runtime boundary is not supported by the current runtime configuration.</summary>
    public const string UnsupportedRuntimeBoundary = "SKR1013";

    /// <summary>An invalid runtime state transition was requested.</summary>
    public const string InvalidRuntimeStateTransition = "SKR1014";

    /// <summary>The scheduler detected a no-progress state.</summary>
    public const string ExecutionNoProgress = "SKR1015";

    /// <summary>A loop node declared invalid or unsupported runtime parameters.</summary>
    public const string InvalidLoopParameters = "SKR1016";

    /// <summary>A loop body could not complete through continue, break, terminal, or completed flow.</summary>
    public const string LoopNoProgress = "SKR1017";

    /// <summary>A workflow invocation reference could not be resolved.</summary>
    public const string WorkflowInvocationNotFound = "SKR1018";

    /// <summary>A workflow invocation failed and propagated to the parent invocation.</summary>
    public const string WorkflowInvocationFailed = "SKR1019";

    /// <summary>A required runtime resource provider was unavailable.</summary>
    public const string RuntimeResourceProviderUnavailable = "SKR1020";

    /// <summary>A runtime resource provider returned an invalid resource instance.</summary>
    public const string RuntimeResourceProviderInvalid = "SKR1021";

    /// <summary>An in-memory interaction continuation was rejected.</summary>
    public const string InteractionContinuationRejected = "SKR1022";

    /// <summary>A node handler exceeded its declared execution timeout.</summary>
    public const string NodeExecutionTimedOut = "SKR1023";

    /// <summary>A node failure stopped execution through its explicit on-error policy.</summary>
    public const string NodeExecutionStopped = "SKR1024";
}
