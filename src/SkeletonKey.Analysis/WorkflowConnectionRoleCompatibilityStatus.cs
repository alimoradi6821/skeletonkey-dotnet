namespace SkeletonKey.Analysis;

/// <summary>
/// Describes role compatibility status for a catalog-aware connection.
/// </summary>
public enum WorkflowConnectionRoleCompatibilityStatus
{
    /// <summary>Connection role compatibility was not analyzed.</summary>
    NotAnalyzed,

    /// <summary>Connection roles are compatible.</summary>
    Compatible,

    /// <summary>A connection endpoint has an invalid port direction.</summary>
    InvalidDirection,

    /// <summary>Connection endpoint roles are incompatible.</summary>
    IncompatibleRole,
}
