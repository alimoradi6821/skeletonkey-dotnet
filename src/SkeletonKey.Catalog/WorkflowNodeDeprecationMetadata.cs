namespace SkeletonKey.Catalog;

/// <summary>
/// Describes optional node definition deprecation metadata.
/// </summary>
public sealed class WorkflowNodeDeprecationMetadata
{
    /// <summary>
    /// Initializes deprecation metadata.
    /// </summary>
    /// <param name="deprecated">Whether this definition is deprecated.</param>
    /// <param name="sinceVersion">Optional catalog or node version where deprecation began.</param>
    /// <param name="message">Optional human-readable deprecation message.</param>
    /// <param name="replacementType">Optional replacement node type.</param>
    /// <param name="replacementVersion">Optional replacement node version.</param>
    public WorkflowNodeDeprecationMetadata(
        bool deprecated = false,
        string? sinceVersion = null,
        string? message = null,
        string? replacementType = null,
        int? replacementVersion = null)
    {
        Deprecated = deprecated;
        SinceVersion = sinceVersion;
        Message = message;
        ReplacementType = replacementType;
        ReplacementVersion = replacementVersion;
    }

    /// <summary>
    /// Gets a value indicating whether this definition is deprecated.
    /// </summary>
    public bool Deprecated { get; }

    /// <summary>
    /// Gets the optional catalog or node version where deprecation began.
    /// </summary>
    public string? SinceVersion { get; }

    /// <summary>
    /// Gets an optional human-readable deprecation message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets an optional replacement node type.
    /// </summary>
    public string? ReplacementType { get; }

    /// <summary>
    /// Gets an optional replacement node version.
    /// </summary>
    public int? ReplacementVersion { get; }
}
