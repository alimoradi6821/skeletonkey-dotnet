namespace SkeletonKey.Planning;

/// <summary>
/// Describes optional boundary metadata for planned steps.
/// </summary>
public sealed class WorkflowExecutionPlanBoundary
{
    private static readonly IReadOnlyDictionary<string, string> _emptyMetadata = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a planning boundary.
    /// </summary>
    /// <param name="id">The boundary identifier.</param>
    /// <param name="kind">The boundary kind.</param>
    /// <param name="parentId">Optional parent boundary identifier.</param>
    /// <param name="metadata">Deterministic boundary metadata, such as branch ports, iteration IDs, or opaque child workflow references.</param>
    public WorkflowExecutionPlanBoundary(string id, string kind, string? parentId = null, IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = id;
        Kind = kind;
        ParentId = parentId;
        Metadata = metadata is null ? _emptyMetadata : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the boundary identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the boundary kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets an optional parent boundary identifier.
    /// </summary>
    public string? ParentId { get; }

    /// <summary>
    /// Gets deterministic boundary metadata without live runtime state or handler references.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
