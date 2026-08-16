using System.Collections.ObjectModel;

namespace SkeletonKey.Workflow.Invocation;

/// <summary>
/// Represents an immutable workflow invocation stream propagation policy.
/// </summary>
/// <remarks>
/// The policy only declares intended stream forwarding behavior. It does not dispatch, suppress, or map events.
/// </remarks>
public sealed class WorkflowInvocationStreamPolicy
{
    private static readonly IReadOnlyDictionary<string, string> _emptyMappings = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>
    /// Initializes a new invocation stream policy.
    /// </summary>
    /// <param name="mode">The stream propagation mode.</param>
    /// <param name="mappings">Optional source-to-target stream channel mappings.</param>
    public WorkflowInvocationStreamPolicy(
        WorkflowInvocationStreamMode mode = WorkflowInvocationStreamMode.Forward,
        IReadOnlyDictionary<string, string>? mappings = null)
    {
        Mode = mode;
        Mappings = mappings is null
            ? _emptyMappings
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(mappings));
    }

    /// <summary>
    /// Gets the stream propagation mode.
    /// </summary>
    public WorkflowInvocationStreamMode Mode { get; }

    /// <summary>
    /// Gets source-to-target stream channel mappings.
    /// </summary>
    public IReadOnlyDictionary<string, string> Mappings { get; }
}
