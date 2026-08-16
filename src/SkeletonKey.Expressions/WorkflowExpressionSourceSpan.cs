namespace SkeletonKey.Expressions;

/// <summary>
/// Identifies a deterministic source span inside expression text.
/// </summary>
/// <param name="Offset">The zero-based character offset of the span.</param>
/// <param name="Length">The character length of the span.</param>
public readonly record struct WorkflowExpressionSourceSpan(int Offset, int Length);
