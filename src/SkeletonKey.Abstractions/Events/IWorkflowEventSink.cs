namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Defines a host-neutral sink for workflow execution events.
/// </summary>
public interface IWorkflowEventSink
{
    /// <summary>
    /// Publishes a workflow execution event.
    /// </summary>
    /// <param name="workflowEvent">The workflow event to publish.</param>
    /// <param name="cancellationToken">A token used to cancel publication.</param>
    /// <returns>A value task that completes when publication is accepted by the sink.</returns>
    public ValueTask PublishAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default);
}
