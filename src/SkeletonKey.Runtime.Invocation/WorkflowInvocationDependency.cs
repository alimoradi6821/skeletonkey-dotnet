using SkeletonKey.Workflow.References;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>Describes one resolved edge in a cross-workflow invocation graph.</summary>
public sealed class WorkflowInvocationDependency
{
    /// <summary>Initializes a resolved invocation dependency.</summary>
    public WorkflowInvocationDependency(string parentWorkflowId, string nodeId, WorkflowReference reference, string childWorkflowId, int depth)
    {
        ParentWorkflowId = parentWorkflowId;
        NodeId = nodeId;
        Reference = reference;
        ChildWorkflowId = childWorkflowId;
        Depth = depth;
    }

    /// <summary>Gets the parent workflow identifier.</summary>
    public string ParentWorkflowId { get; }

    /// <summary>Gets the invocation node identifier.</summary>
    public string NodeId { get; }

    /// <summary>Gets the exact declared workflow reference.</summary>
    public WorkflowReference Reference { get; }

    /// <summary>Gets the resolved child workflow identifier.</summary>
    public string ChildWorkflowId { get; }

    /// <summary>Gets the child depth below the root workflow.</summary>
    public int Depth { get; }
}
