namespace SkeletonKey.Runtime.Invocation;

/// <summary>Describes one deterministic cross-workflow invocation error.</summary>
public sealed class WorkflowInvocationAnalysisIssue
{
    /// <summary>Initializes an invocation analysis issue.</summary>
    public WorkflowInvocationAnalysisIssue(string code, string message, string workflowId, string nodeId, string path)
    {
        Code = code;
        Message = message;
        WorkflowId = workflowId;
        NodeId = nodeId;
        Path = path;
    }

    /// <summary>Gets the stable issue code.</summary>
    public string Code { get; }

    /// <summary>Gets the host-neutral issue message.</summary>
    public string Message { get; }

    /// <summary>Gets the parent workflow identifier.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the invocation node identifier.</summary>
    public string NodeId { get; }

    /// <summary>Gets the JSON Pointer-like location in the parent workflow.</summary>
    public string Path { get; }
}
