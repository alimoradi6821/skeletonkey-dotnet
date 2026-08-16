using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Runtime.Resources;

/// <summary>
/// Describes one runtime request to create a host-neutral workflow resource instance.
/// </summary>
public sealed class WorkflowRuntimeResourceRequest
{
    /// <summary>
    /// Initializes a runtime resource creation request.
    /// </summary>
    public WorkflowRuntimeResourceRequest(
        string executionId,
        string invocationId,
        string workflowId,
        string resourceName,
        WorkflowResourceDefinition definition)
    {
        ExecutionId = executionId;
        InvocationId = invocationId;
        WorkflowId = workflowId;
        ResourceName = resourceName;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>Gets the root execution identifier.</summary>
    public string ExecutionId { get; }

    /// <summary>Gets the owning invocation identifier.</summary>
    public string InvocationId { get; }

    /// <summary>Gets the workflow identifier containing the declaration.</summary>
    public string WorkflowId { get; }

    /// <summary>Gets the workflow resource declaration name.</summary>
    public string ResourceName { get; }

    /// <summary>Gets the immutable workflow resource definition.</summary>
    public WorkflowResourceDefinition Definition { get; }
}
