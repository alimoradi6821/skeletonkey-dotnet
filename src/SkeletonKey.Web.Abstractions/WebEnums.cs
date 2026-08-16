namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Defines supported navigation load-state waits.
/// </summary>
public enum WebNavigationWaitUntil
{
    /// <summary>Wait for commit only.</summary>
    Commit,

    /// <summary>Wait for DOM content loaded.</summary>
    DomContentLoaded,

    /// <summary>Wait for the load event.</summary>
    Load,

    /// <summary>Wait for the network idle state.</summary>
    NetworkIdle,
}

/// <summary>
/// Defines supported locator state waits.
/// </summary>
public enum WebWaitState
{
    /// <summary>Wait until attached.</summary>
    Attached,

    /// <summary>Wait until visible.</summary>
    Visible,

    /// <summary>Wait until hidden.</summary>
    Hidden,

    /// <summary>Wait until detached.</summary>
    Detached,
}

/// <summary>
/// Defines supported screenshot image formats.
/// </summary>
public enum WebScreenshotFormat
{
    /// <summary>PNG image data.</summary>
    Png,

    /// <summary>JPEG image data.</summary>
    Jpeg,
}
