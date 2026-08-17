namespace SkeletonKey.Runtime;

/// <summary>
/// Provides host-neutral asynchronous waiting for runtime-owned retry scheduling.
/// </summary>
/// <remarks>
/// Implementations must honor cancellation. The runtime supplies the exact bounded delay; implementations do not calculate policy backoff.
/// </remarks>
public interface IWorkflowRuntimeDelay
{
    /// <summary>Waits for the supplied retry delay.</summary>
    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
