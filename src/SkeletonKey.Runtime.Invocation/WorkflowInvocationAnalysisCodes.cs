namespace SkeletonKey.Runtime.Invocation;

/// <summary>Stable codes produced by cross-workflow invocation analysis.</summary>
public static class WorkflowInvocationAnalysisCodes
{
    /// <summary>The referenced workflow could not be resolved.</summary>
    public const string WorkflowNotFound = "SKD1001";

    /// <summary>The repository returned a workflow with an unexpected identifier.</summary>
    public const string WorkflowIdentityMismatch = "SKD1002";

    /// <summary>The invocation graph contains direct or indirect recursion.</summary>
    public const string InvocationCycle = "SKD1003";

    /// <summary>The invocation graph exceeds the configured depth limit.</summary>
    public const string InvocationDepthExceeded = "SKD1004";

    /// <summary>A required child workflow input is not supplied.</summary>
    public const string RequiredChildInputMissing = "SKD1005";

    /// <summary>An invocation supplies an input not declared by the child workflow.</summary>
    public const string UnknownChildInput = "SKD1006";

    /// <summary>A static invocation input is incompatible with the child input type.</summary>
    public const string ChildInputTypeMismatch = "SKD1007";

    /// <summary>A mapped child stream channel is not declared by the child workflow.</summary>
    public const string UnknownChildStreamChannel = "SKD1008";

    /// <summary>The repository failed while resolving an invocation dependency.</summary>
    public const string RepositoryFailure = "SKD1009";
}
