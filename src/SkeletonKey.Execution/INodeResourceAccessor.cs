namespace SkeletonKey.Execution;

/// <summary>
/// Defines scoped node resource access by declared resource slot.
/// </summary>
/// <remarks>
/// This contract forbids arbitrary workflow resource-name lookup, hidden service location, resolution implementation, and locking implementation.
/// Missing optional resources are reserved for a future unavailable-resource result or exception contract.
/// </remarks>
public interface INodeResourceAccessor
{
    /// <summary>
    /// Gets immutable planned resource bindings visible to the node handler.
    /// </summary>
    public IReadOnlyList<NodeResourceBinding> Bindings { get; }

    /// <summary>
    /// Attempts to get one planned binding by declared node resource slot name.
    /// </summary>
    /// <param name="slotName">The declared node resource slot name.</param>
    /// <param name="binding">When successful, receives the planned binding for the slot.</param>
    /// <returns><see langword="true" /> when the binding exists; otherwise, <see langword="false" />.</returns>
    public bool TryGetBinding(
        string slotName,
        out NodeResourceBinding? binding);

    /// <summary>
    /// Acquires a scoped resource lease by declared node resource slot name.
    /// </summary>
    /// <param name="slotName">The declared node resource slot name.</param>
    /// <param name="cancellationToken">A token the future runtime supplies for cancellation.</param>
    /// <returns>The acquired resource lease scoped to the declared slot.</returns>
    public ValueTask<INodeResourceLease> AcquireAsync(
        string slotName,
        CancellationToken cancellationToken = default);
}
