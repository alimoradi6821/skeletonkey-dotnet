namespace SkeletonKey.Expressions;

/// <summary>
/// Represents an immutable expression parsing diagnostic with a deterministic source span.
/// </summary>
public sealed class WorkflowExpressionDiagnostic
{
    /// <summary>
    /// Initializes a new expression diagnostic.
    /// </summary>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">A concise deterministic diagnostic message.</param>
    /// <param name="offset">The zero-based source offset.</param>
    /// <param name="length">The source length associated with the diagnostic.</param>
    public WorkflowExpressionDiagnostic(string code, string message, int offset, int length)
    {
        Code = code;
        Message = message;
        SourceSpan = new WorkflowExpressionSourceSpan(offset, length);
    }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets a concise deterministic diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the deterministic source span for the diagnostic.
    /// </summary>
    public WorkflowExpressionSourceSpan SourceSpan { get; }

    /// <summary>
    /// Gets the zero-based source offset.
    /// </summary>
    public int Offset => SourceSpan.Offset;

    /// <summary>
    /// Gets the source length associated with the diagnostic.
    /// </summary>
    public int Length => SourceSpan.Length;
}
