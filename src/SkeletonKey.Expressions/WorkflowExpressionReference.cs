namespace SkeletonKey.Expressions;

/// <summary>
/// Represents an immutable statically discovered workflow data reference.
/// </summary>
public sealed class WorkflowExpressionReference
{
    /// <summary>
    /// Initializes a new expression reference.
    /// </summary>
    /// <param name="kind">The workflow reference kind.</param>
    /// <param name="root">The root identifier used by the expression.</param>
    /// <param name="referencedName">The input or variable name when applicable.</param>
    /// <param name="nodeId">The referenced node identifier when applicable.</param>
    /// <param name="port">The referenced node output port when applicable.</param>
    /// <param name="iterationId">The explicit iteration identifier when applicable.</param>
    /// <param name="sourceSpan">The deterministic source span for the reference.</param>
    public WorkflowExpressionReference(
        WorkflowExpressionReferenceKind kind,
        string root,
        string? referencedName,
        string? nodeId,
        string? port,
        string? iterationId,
        WorkflowExpressionSourceSpan sourceSpan)
    {
        Kind = kind;
        Root = root;
        ReferencedName = referencedName;
        NodeId = nodeId;
        Port = port;
        IterationId = iterationId;
        SourceSpan = sourceSpan;
    }

    /// <summary>
    /// Gets the workflow reference kind.
    /// </summary>
    public WorkflowExpressionReferenceKind Kind { get; }

    /// <summary>
    /// Gets the expression root identifier.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the referenced input or variable name when applicable.
    /// </summary>
    public string? ReferencedName { get; }

    /// <summary>
    /// Gets the referenced node identifier when applicable.
    /// </summary>
    public string? NodeId { get; }

    /// <summary>
    /// Gets the referenced node output port when applicable.
    /// </summary>
    public string? Port { get; }

    /// <summary>
    /// Gets the explicit iteration identifier when applicable.
    /// </summary>
    public string? IterationId { get; }

    /// <summary>
    /// Gets the deterministic source span for the reference.
    /// </summary>
    public WorkflowExpressionSourceSpan SourceSpan { get; }
}
