namespace SkeletonKey.Desktop.Abstractions;

/// <summary>Represents an expected structured desktop automation failure.</summary>
public sealed class DesktopAutomationException : Exception
{
    /// <summary>Initializes a desktop automation exception.</summary>
    public DesktopAutomationException(DesktopOperationError error, Exception? innerException = null)
        : base(error?.Message, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>Gets the structured desktop operation error.</summary>
    public DesktopOperationError Error { get; }
}

/// <summary>Describes one provider-neutral desktop automation error.</summary>
public sealed record DesktopOperationError(string Code, string Message, string Operation);
