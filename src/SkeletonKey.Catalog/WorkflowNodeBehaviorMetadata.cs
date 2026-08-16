namespace SkeletonKey.Catalog;

/// <summary>
/// Describes high-level non-executable behavior metadata for a node definition.
/// </summary>
public sealed class WorkflowNodeBehaviorMetadata
{
    /// <summary>
    /// Initializes node behavior metadata.
    /// </summary>
    /// <param name="kind">The behavior kind.</param>
    /// <param name="terminal">Whether the node terminates its active flow path.</param>
    /// <param name="maySuspend">Whether the node may suspend awaiting external input in a future runtime.</param>
    /// <param name="description">Optional human-readable behavior description.</param>
    public WorkflowNodeBehaviorMetadata(
        WorkflowNodeBehaviorKind kind = WorkflowNodeBehaviorKind.Action,
        bool terminal = false,
        bool maySuspend = false,
        string? description = null)
    {
        Kind = kind;
        Terminal = terminal;
        MaySuspend = maySuspend;
        Description = description;
    }

    /// <summary>
    /// Gets the behavior kind.
    /// </summary>
    public WorkflowNodeBehaviorKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the node terminates its active flow path.
    /// </summary>
    public bool Terminal { get; }

    /// <summary>
    /// Gets a value indicating whether the node may suspend awaiting external input in a future runtime.
    /// </summary>
    public bool MaySuspend { get; }

    /// <summary>
    /// Gets an optional human-readable behavior description.
    /// </summary>
    public string? Description { get; }
}
