namespace SkeletonKey.Runtime;

/// <summary>Stable error codes for durable workflow checkpoint operations.</summary>
public static class WorkflowCheckpointErrorCodes
{
    /// <summary>The checkpoint format version is not supported.</summary>
    public const string UnsupportedFormatVersion = "SKR3001";

    /// <summary>The checkpoint identity does not match the requested execution, workflow, or plan.</summary>
    public const string IdentityMismatch = "SKR3002";

    /// <summary>The checkpoint payload or integrity metadata is invalid.</summary>
    public const string InvalidCheckpoint = "SKR3003";

    /// <summary>The persisted revision changed since it was loaded.</summary>
    public const string RevisionConflict = "SKR3004";

    /// <summary>The checkpoint could not be read or written.</summary>
    public const string StoreFailure = "SKR3005";

    /// <summary>A process stopped while a node was running and explicit recovery is required.</summary>
    public const string InterruptedStepRequiresRecovery = "SKR3006";

    /// <summary>The checkpoint does not contain the exact planned step set.</summary>
    public const string PlanShapeMismatch = "SKR3007";

    /// <summary>The workflow uses runtime resources whose live handles cannot be resumed.</summary>
    public const string ResourceResumeNotSupported = "SKR3008";

    /// <summary>A resumable runtime resource could not be captured or reconstructed.</summary>
    public const string ResourceRecoveryFailed = "SKR3009";
}
