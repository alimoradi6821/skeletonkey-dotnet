namespace SkeletonKey.Runtime;

/// <summary>
/// Identifies one runtime activation of a planned workflow step.
/// </summary>
/// <remarks>
/// Activations distinguish repeated loop body executions from handler retry attempts. The identity is host-neutral and contains no scheduler state.
/// </remarks>
public sealed class RuntimeActivationIdentity : IEquatable<RuntimeActivationIdentity>
{
    /// <summary>
    /// Initializes a runtime activation identity.
    /// </summary>
    /// <param name="frameId">The runtime frame containing the activation.</param>
    /// <param name="activationId">The deterministic activation identifier.</param>
    /// <param name="parentActivationId">The optional parent activation identifier.</param>
    /// <param name="stepId">The execution plan step identifier.</param>
    /// <param name="nodeId">The workflow node identifier.</param>
    /// <param name="ordinal">The one-based activation ordinal for this node within the invocation.</param>
    public RuntimeActivationIdentity(
        string frameId,
        string activationId,
        string? parentActivationId,
        string stepId,
        string nodeId,
        long ordinal)
    {
        if (ordinal < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Activation ordinal must be one-based.");
        }

        FrameId = frameId;
        ActivationId = activationId;
        ParentActivationId = parentActivationId;
        StepId = stepId;
        NodeId = nodeId;
        Ordinal = ordinal;
    }

    /// <summary>Gets the runtime frame containing the activation.</summary>
    public string FrameId { get; }

    /// <summary>Gets the deterministic activation identifier.</summary>
    public string ActivationId { get; }

    /// <summary>Gets the optional parent activation identifier.</summary>
    public string? ParentActivationId { get; }

    /// <summary>Gets the execution plan step identifier.</summary>
    public string StepId { get; }

    /// <summary>Gets the workflow node identifier.</summary>
    public string NodeId { get; }

    /// <summary>Gets the one-based activation ordinal for this node within the invocation.</summary>
    public long Ordinal { get; }

    /// <inheritdoc />
    public bool Equals(RuntimeActivationIdentity? other)
    {
        return other is not null &&
            string.Equals(FrameId, other.FrameId, StringComparison.Ordinal) &&
            string.Equals(ActivationId, other.ActivationId, StringComparison.Ordinal) &&
            string.Equals(ParentActivationId, other.ParentActivationId, StringComparison.Ordinal) &&
            string.Equals(StepId, other.StepId, StringComparison.Ordinal) &&
            string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
            Ordinal == other.Ordinal;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as RuntimeActivationIdentity);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        hashCode.Add(FrameId, StringComparer.Ordinal);
        hashCode.Add(ActivationId, StringComparer.Ordinal);
        hashCode.Add(ParentActivationId, StringComparer.Ordinal);
        hashCode.Add(StepId, StringComparer.Ordinal);
        hashCode.Add(NodeId, StringComparer.Ordinal);
        hashCode.Add(Ordinal);
        return hashCode.ToHashCode();
    }
}
