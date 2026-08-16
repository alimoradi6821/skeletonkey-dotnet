namespace SkeletonKey.Artifacts;

/// <summary>
/// Represents a structured artifact-store failure.
/// </summary>
public sealed class WorkflowArtifactException : Exception
{
    /// <summary>
    /// Initializes an artifact exception.
    /// </summary>
    public WorkflowArtifactException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Gets the stable artifact error code.</summary>
    public string Code { get; }
}
