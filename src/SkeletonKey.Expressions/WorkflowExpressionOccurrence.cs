namespace SkeletonKey.Expressions;

/// <summary>
/// Represents one expression wrapper occurrence discovered in a workflow value.
/// </summary>
/// <param name="Path">The JSON Pointer path to the expression wrapper.</param>
/// <param name="Text">The exact expression text.</param>
public sealed record WorkflowExpressionOccurrence(string Path, string Text);
