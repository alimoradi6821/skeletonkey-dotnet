using SkeletonKey.Runtime;

namespace SkeletonKey.Runtime.Default;

/// <summary>
/// Provides UTC timestamps from the system clock for runtime-owned state, metrics, and event sequencing.
/// </summary>
/// <remarks>
/// This adapter is not used to generate execution IDs, plan IDs, random values, expression values, or handler-visible clocks.
/// </remarks>
public sealed class SystemWorkflowClock : IWorkflowClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
