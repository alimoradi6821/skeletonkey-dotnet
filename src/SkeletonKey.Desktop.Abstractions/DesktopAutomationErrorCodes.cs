namespace SkeletonKey.Desktop.Abstractions;

/// <summary>Defines stable provider-neutral desktop automation error codes.</summary>
public static class DesktopAutomationErrorCodes
{
    /// <summary>Desktop application resource unavailable.</summary>
    public const string ApplicationResourceUnavailable = "SKR2301";

    /// <summary>Desktop automation is unavailable on the current operating system.</summary>
    public const string PlatformNotSupported = "SKR2302";

    /// <summary>Application launch or attachment failed.</summary>
    public const string ApplicationStartFailed = "SKR2303";

    /// <summary>The application main window did not become available.</summary>
    public const string WindowUnavailable = "SKR2304";

    /// <summary>The locator uses a strategy unsupported by the desktop provider.</summary>
    public const string UnsupportedLocatorStrategy = "SKR2305";

    /// <summary>No desktop element satisfied the resolved locator.</summary>
    public const string LocatorNotFound = "SKR2306";

    /// <summary>Desktop element matches did not satisfy declared cardinality or index.</summary>
    public const string LocatorCardinalityMismatch = "SKR2307";

    /// <summary>A desktop locator operation exceeded its timeout.</summary>
    public const string LocatorOperationTimeout = "SKR2308";

    /// <summary>A desktop action failed.</summary>
    public const string DesktopActionFailed = "SKR2309";

    /// <summary>A desktop operation was cancelled.</summary>
    public const string DesktopOperationCancelled = "SKR2310";
}
