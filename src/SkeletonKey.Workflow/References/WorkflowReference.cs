namespace SkeletonKey.Workflow.References;

/// <summary>
/// Identifies a workflow to be invoked by another workflow.
/// </summary>
/// <remarks>
/// The version, when present, is an exact Semantic Version 2.0 value. The contract does not define
/// package, registry, file, remote lookup, or version-range resolution behavior.
/// </remarks>
public sealed class WorkflowReference
{
    /// <summary>
    /// Initializes a new workflow reference.
    /// </summary>
    /// <param name="id">The referenced workflow identifier.</param>
    /// <param name="version">Optional exact Semantic Version 2.0 workflow version.</param>
    public WorkflowReference(string id, string? version = null)
    {
        Id = id;
        Version = version;
    }

    /// <summary>
    /// Gets the referenced workflow identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the optional exact Semantic Version 2.0 workflow version.
    /// </summary>
    public string? Version { get; }
}
