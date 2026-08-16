namespace SkeletonKey.Expressions;

/// <summary>
/// Defines the supported safe workflow data roots referenced by an expression.
/// </summary>
public enum WorkflowExpressionReferenceKind
{
    /// <summary>
    /// A reference to a workflow input.
    /// </summary>
    Input,

    /// <summary>
    /// A reference to a workflow variable.
    /// </summary>
    Variable,

    /// <summary>
    /// A reference to a workflow node output.
    /// </summary>
    Node,

    /// <summary>
    /// A reference to an explicit iteration context.
    /// </summary>
    Iteration,
}
