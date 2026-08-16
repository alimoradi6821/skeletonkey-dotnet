namespace SkeletonKey.Validation;

/// <summary>
/// Defines semantic validation issue severity.
/// </summary>
public enum WorkflowValidationSeverity
{
    /// <summary>
    /// Indicates a semantic problem that makes the workflow invalid.
    /// </summary>
    Error,

    /// <summary>
    /// Indicates a non-fatal semantic concern that does not make the workflow invalid.
    /// </summary>
    Warning,
}
