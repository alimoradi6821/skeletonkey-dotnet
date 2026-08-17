namespace SkeletonKey.Runtime;

/// <summary>
/// Represents immutable host-neutral runtime limits and behavior switches.
/// </summary>
/// <remarks>
/// Options provide deterministic safe defaults and intentionally exclude workflow-declared policy values, persistence, browser behavior, dependency injection,
/// host clocks for identity creation, and any host-dependent defaults.
/// </remarks>
public sealed class WorkflowRuntimeOptions
{
    /// <summary>
    /// Initializes a new runtime options instance.
    /// </summary>
    /// <param name="maximumExecutedNodeAttempts">The maximum number of node attempts the scheduler may execute in one workflow execution.</param>
    /// <param name="maximumReadySteps">The maximum number of simultaneously ready steps retained by the deterministic scheduler.</param>
    /// <param name="maximumStoredNodeResults">The maximum number of terminal node results retained in the runtime result.</param>
    /// <param name="stopOnFirstUnhandledFailure">Whether the runtime stops the root execution when a reachable node fails without handling.</param>
    /// <param name="emitStateTransitionEvents">Whether lifecycle transition events are emitted through the runtime event coordinator.</param>
    /// <param name="maximumLoopIterationsPerNode">The maximum number of iterations any loop node may execute.</param>
    /// <param name="maximumNestedLoopDepth">The maximum nested runtime loop frame depth.</param>
    /// <param name="maximumRuntimeActivations">The maximum number of runtime step activations in one execution.</param>
    /// <param name="maximumInvocationDepth">The maximum nested workflow invocation depth.</param>
    /// <param name="maximumInvocations">The maximum number of workflow invocations in one root execution.</param>
    public WorkflowRuntimeOptions(
        int maximumExecutedNodeAttempts = 10_000,
        int maximumReadySteps = 10_000,
        int maximumStoredNodeResults = 10_000,
        bool stopOnFirstUnhandledFailure = true,
        bool emitStateTransitionEvents = true,
        int maximumLoopIterationsPerNode = 10_000,
        int maximumNestedLoopDepth = 128,
        int maximumRuntimeActivations = 100_000,
        int maximumInvocationDepth = 64,
        int maximumInvocations = 10_000)
    {
        if (maximumExecutedNodeAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExecutedNodeAttempts), maximumExecutedNodeAttempts, "The attempt limit must be positive.");
        }

        if (maximumReadySteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReadySteps), maximumReadySteps, "The ready-step limit must be positive.");
        }

        if (maximumStoredNodeResults < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumStoredNodeResults), maximumStoredNodeResults, "The stored-result limit must be positive.");
        }

        if (maximumLoopIterationsPerNode < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLoopIterationsPerNode), maximumLoopIterationsPerNode, "The per-loop iteration limit must be positive.");
        }

        if (maximumNestedLoopDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumNestedLoopDepth), maximumNestedLoopDepth, "The nested-loop depth limit must be positive.");
        }

        if (maximumRuntimeActivations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRuntimeActivations), maximumRuntimeActivations, "The runtime activation limit must be positive.");
        }

        if (maximumInvocationDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumInvocationDepth), maximumInvocationDepth, "The invocation depth limit must be positive.");
        }

        if (maximumInvocations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumInvocations), maximumInvocations, "The invocation limit must be positive.");
        }

        MaximumExecutedNodeAttempts = maximumExecutedNodeAttempts;
        MaximumReadySteps = maximumReadySteps;
        MaximumStoredNodeResults = maximumStoredNodeResults;
        StopOnFirstUnhandledFailure = stopOnFirstUnhandledFailure;
        EmitStateTransitionEvents = emitStateTransitionEvents;
        MaximumLoopIterationsPerNode = maximumLoopIterationsPerNode;
        MaximumNestedLoopDepth = maximumNestedLoopDepth;
        MaximumRuntimeActivations = maximumRuntimeActivations;
        MaximumInvocationDepth = maximumInvocationDepth;
        MaximumInvocations = maximumInvocations;
    }

    /// <summary>
    /// Gets the maximum number of node attempts the scheduler may execute in one workflow execution.
    /// </summary>
    public int MaximumExecutedNodeAttempts { get; }

    /// <summary>
    /// Gets the maximum number of simultaneously ready steps retained by the deterministic scheduler.
    /// </summary>
    public int MaximumReadySteps { get; }

    /// <summary>
    /// Gets the maximum number of terminal node results retained in the runtime result.
    /// </summary>
    public int MaximumStoredNodeResults { get; }

    /// <summary>
    /// Gets a value indicating whether the runtime stops the root execution when a reachable node fails without handling.
    /// </summary>
    public bool StopOnFirstUnhandledFailure { get; }

    /// <summary>
    /// Gets a value indicating whether lifecycle transition events are emitted through the runtime event coordinator.
    /// </summary>
    public bool EmitStateTransitionEvents { get; }

    /// <summary>
    /// Gets the maximum number of iterations any loop node may execute.
    /// </summary>
    public int MaximumLoopIterationsPerNode { get; }

    /// <summary>
    /// Gets the maximum nested runtime loop frame depth.
    /// </summary>
    public int MaximumNestedLoopDepth { get; }

    /// <summary>
    /// Gets the maximum number of runtime step activations in one execution.
    /// </summary>
    public int MaximumRuntimeActivations { get; }

    /// <summary>
    /// Gets the maximum nested workflow invocation depth.
    /// </summary>
    public int MaximumInvocationDepth { get; }

    /// <summary>
    /// Gets the maximum number of workflow invocations in one root execution.
    /// </summary>
    public int MaximumInvocations { get; }
}
