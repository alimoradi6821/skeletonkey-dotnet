namespace SkeletonKey.Analysis;

/// <summary>
/// Identifies whether an effective port came directly from catalog metadata or deterministic parameter data.
/// </summary>
public enum WorkflowEffectivePortOrigin
{
    /// <summary>The port is declared statically by the resolved node definition.</summary>
    Static,

    /// <summary>The port is derived from literal workflow node parameters through catalog dynamic-port metadata.</summary>
    Dynamic,
}
