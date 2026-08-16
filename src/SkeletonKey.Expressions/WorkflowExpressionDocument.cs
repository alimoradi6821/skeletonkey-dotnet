using System.Collections.ObjectModel;

namespace SkeletonKey.Expressions;

/// <summary>
/// Represents an immutable parsed expression document without evaluation behavior.
/// </summary>
public sealed class WorkflowExpressionDocument
{
    /// <summary>
    /// Initializes a new parsed expression document.
    /// </summary>
    /// <param name="originalText">The exact expression text supplied by the workflow document.</param>
    /// <param name="diagnostics">Deterministic parse diagnostics.</param>
    /// <param name="references">Deterministically discovered workflow data references.</param>
    public WorkflowExpressionDocument(
        string originalText,
        IReadOnlyList<WorkflowExpressionDiagnostic>? diagnostics = null,
        IReadOnlyList<WorkflowExpressionReference>? references = null)
    {
        OriginalText = originalText;
        Diagnostics = new ReadOnlyCollection<WorkflowExpressionDiagnostic>([.. diagnostics ?? []]);
        References = new ReadOnlyCollection<WorkflowExpressionReference>([.. references ?? []]);
    }

    /// <summary>
    /// Gets the exact expression text. The parser never normalizes or reformats it.
    /// </summary>
    public string OriginalText { get; }

    /// <summary>
    /// Gets deterministic parse diagnostics.
    /// </summary>
    public IReadOnlyList<WorkflowExpressionDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets statically discovered workflow references without evaluating member paths.
    /// </summary>
    public IReadOnlyList<WorkflowExpressionReference> References { get; }

    /// <summary>
    /// Gets a value indicating whether parsing completed without diagnostics.
    /// </summary>
    public bool IsValid => Diagnostics.Count == 0;
}
