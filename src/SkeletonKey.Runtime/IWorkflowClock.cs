namespace SkeletonKey.Runtime;

/// <summary>
/// Provides host-neutral UTC timestamps for runtime-owned events, state transitions, and metrics.
/// </summary>
/// <remarks>
/// The clock is never used for execution identity generation, expression evaluation, handler behavior, randomness, persistence, or browser behavior.
/// </remarks>
public interface IWorkflowClock
{
    /// <summary>
    /// Gets the current UTC timestamp.
    /// </summary>
    public DateTimeOffset UtcNow { get; }
}
