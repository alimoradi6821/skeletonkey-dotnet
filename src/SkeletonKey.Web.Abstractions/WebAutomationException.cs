namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Represents an expected structured web automation failure.
/// </summary>
public sealed class WebAutomationException : Exception
{
    /// <summary>
    /// Initializes a web automation exception.
    /// </summary>
    public WebAutomationException(WebOperationError error, Exception? innerException = null)
        : base(error?.Message, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>Gets the structured web operation error.</summary>
    public WebOperationError Error { get; }
}
