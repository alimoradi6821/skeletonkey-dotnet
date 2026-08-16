using SkeletonKey.Catalog;

namespace SkeletonKey.Execution;

/// <summary>
/// Identifies one exact node execution attempt within a workflow execution plan.
/// </summary>
/// <remarks>
/// All string identity comparisons are ordinal and case-sensitive. Identifiers are supplied by a future runtime; this type does not generate IDs.
/// </remarks>
public sealed class NodeExecutionIdentity : IEquatable<NodeExecutionIdentity>
{
    /// <summary>
    /// Initializes a new node execution attempt identity.
    /// </summary>
    /// <param name="executionId">The identifier for the complete root execution.</param>
    /// <param name="invocationId">The identifier for the current workflow invocation.</param>
    /// <param name="parentInvocationId">The optional parent invocation identifier; root invocations use <see langword="null" />.</param>
    /// <param name="workflowId">The identifier for the current workflow.</param>
    /// <param name="nodeId">The identifier for the workflow node.</param>
    /// <param name="definition">The exact catalog node definition identity.</param>
    /// <param name="planId">The identifier for the execution plan.</param>
    /// <param name="stepId">The identifier for the execution plan step.</param>
    /// <param name="attempt">The one-based execution attempt number.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="attempt" /> is less than 1.</exception>
    public NodeExecutionIdentity(
        string executionId,
        string invocationId,
        string? parentInvocationId,
        string workflowId,
        string nodeId,
        WorkflowNodeDefinitionKey definition,
        string planId,
        string stepId,
        int attempt)
    {
        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Attempt must be one-based.");
        }

        ExecutionId = executionId;
        InvocationId = invocationId;
        ParentInvocationId = parentInvocationId;
        WorkflowId = workflowId;
        NodeId = nodeId;
        Definition = definition;
        PlanId = planId;
        StepId = stepId;
        Attempt = attempt;
    }

    /// <summary>
    /// Gets the identifier for the complete root execution.
    /// </summary>
    public string ExecutionId { get; }

    /// <summary>
    /// Gets the identifier for the current workflow invocation.
    /// </summary>
    public string InvocationId { get; }

    /// <summary>
    /// Gets the optional parent invocation identifier.
    /// </summary>
    public string? ParentInvocationId { get; }

    /// <summary>
    /// Gets the identifier for the current workflow.
    /// </summary>
    public string WorkflowId { get; }

    /// <summary>
    /// Gets the identifier for the workflow node.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the exact catalog node definition identity.
    /// </summary>
    public WorkflowNodeDefinitionKey Definition { get; }

    /// <summary>
    /// Gets the identifier for the execution plan.
    /// </summary>
    public string PlanId { get; }

    /// <summary>
    /// Gets the identifier for the execution plan step.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    /// Gets the one-based execution attempt number.
    /// </summary>
    public int Attempt { get; }

    /// <inheritdoc />
    public bool Equals(NodeExecutionIdentity? other)
    {
        return other is not null &&
            string.Equals(ExecutionId, other.ExecutionId, StringComparison.Ordinal) &&
            string.Equals(InvocationId, other.InvocationId, StringComparison.Ordinal) &&
            string.Equals(ParentInvocationId, other.ParentInvocationId, StringComparison.Ordinal) &&
            string.Equals(WorkflowId, other.WorkflowId, StringComparison.Ordinal) &&
            string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
            Definition.Equals(other.Definition) &&
            string.Equals(PlanId, other.PlanId, StringComparison.Ordinal) &&
            string.Equals(StepId, other.StepId, StringComparison.Ordinal) &&
            Attempt == other.Attempt;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as NodeExecutionIdentity);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(ExecutionId, StringComparer.Ordinal);
        hashCode.Add(InvocationId, StringComparer.Ordinal);
        hashCode.Add(ParentInvocationId, StringComparer.Ordinal);
        hashCode.Add(WorkflowId, StringComparer.Ordinal);
        hashCode.Add(NodeId, StringComparer.Ordinal);
        hashCode.Add(Definition);
        hashCode.Add(PlanId, StringComparer.Ordinal);
        hashCode.Add(StepId, StringComparer.Ordinal);
        hashCode.Add(Attempt);
        return hashCode.ToHashCode();
    }
}
