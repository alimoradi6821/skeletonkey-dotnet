namespace SkeletonKey.Abstractions.Events;

/// <summary>
/// Defines host-neutral workflow log event severity levels.
/// </summary>
public enum WorkflowLogLevel
{
    /// <summary>
    /// Diagnostic trace information.
    /// </summary>
    Trace,

    /// <summary>
    /// Diagnostic debug information.
    /// </summary>
    Debug,

    /// <summary>
    /// Informational execution detail.
    /// </summary>
    Information,

    /// <summary>
    /// Warning information that does not necessarily fail execution.
    /// </summary>
    Warning,

    /// <summary>
    /// Error information associated with failed work.
    /// </summary>
    Error,

    /// <summary>
    /// Critical information associated with unrecoverable work.
    /// </summary>
    Critical,
}
