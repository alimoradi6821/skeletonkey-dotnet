namespace SkeletonKey.Handlers;

/// <summary>
/// Defines the completion status reported by a node handler.
/// </summary>
/// <remarks>
/// Durable suspension is not a handler completion result in this contract. Future runtime work owns suspension and resume semantics.
/// </remarks>
public enum NodeHandlerCompletionStatus
{
    /// <summary>
    /// The handler completed the node attempt successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The handler completed with an expected structured node failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The handler observed cancellation and completed the node attempt as cancelled.
    /// </summary>
    Cancelled,
}
