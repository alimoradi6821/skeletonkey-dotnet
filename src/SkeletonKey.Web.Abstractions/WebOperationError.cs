namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Describes one structured provider-neutral web operation error.
/// </summary>
public sealed class WebOperationError
{
    /// <summary>
    /// Initializes a structured web operation error.
    /// </summary>
    public WebOperationError(string code, string message, string? operation = null)
    {
        Code = code;
        Message = message;
        Operation = operation;
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; }

    /// <summary>Gets sanitized diagnostic text.</summary>
    public string Message { get; }

    /// <summary>Gets optional operation identity.</summary>
    public string? Operation { get; }
}
