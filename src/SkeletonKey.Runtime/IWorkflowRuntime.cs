using SkeletonKey.Abstractions.Execution;

namespace SkeletonKey.Runtime;

/// <summary>
/// Executes immutable workflow documents through validation, analysis, planning, deterministic scheduling, handler invocation, and result aggregation.
/// </summary>
/// <remarks>
/// Runtime implementations own scheduling, lifecycle state, identity enrichment, event ordering, parameter preparation, cancellation,
/// failure normalization, and unsupported-boundary handling. Implementations do not imply persistence, browser automation, or plugin discovery.
/// </remarks>
public interface IWorkflowRuntime
{
    /// <summary>
    /// Starts one workflow execution session and returns immediately after in-memory runtime state is initialized.
    /// </summary>
    /// <param name="request">The immutable workflow execution request with caller-supplied execution and plan identities.</param>
    /// <param name="cancellationToken">The cancellation token observed while creating the session.</param>
    /// <returns>A process-local execution session that can be waited, continued, or cancelled.</returns>
    public ValueTask<IWorkflowExecutionSession> StartAsync(
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one workflow request and returns the runtime-owned final result wrapper.
    /// </summary>
    /// <param name="request">The immutable workflow execution request with caller-supplied execution and plan identities.</param>
    /// <param name="cancellationToken">The cancellation token observed before execution, between steps, during event publication, and during handlers.</param>
    /// <returns>A runtime result containing the final workflow result, state snapshots, node results, and terminal diagnostics.</returns>
    public ValueTask<WorkflowRuntimeResult> ExecuteAsync(
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken = default);
}
