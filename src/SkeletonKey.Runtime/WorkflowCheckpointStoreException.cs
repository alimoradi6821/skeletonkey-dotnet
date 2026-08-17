namespace SkeletonKey.Runtime;

/// <summary>Represents a stable checkpoint persistence failure.</summary>
public sealed class WorkflowCheckpointStoreException : Exception
{
    /// <summary>Initializes a checkpoint persistence exception.</summary>
    public WorkflowCheckpointStoreException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the stable checkpoint error code.</summary>
    public string Code { get; }
}
