namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Defines stable provider-neutral web automation error codes.
/// </summary>
public static class WebAutomationErrorCodes
{
    /// <summary>Web page resource unavailable.</summary>
    public const string PageResourceUnavailable = "SKR2001";

    /// <summary>Browser launch failed.</summary>
    public const string BrowserLaunchFailed = "SKR2002";

    /// <summary>Browser context creation failed.</summary>
    public const string BrowserContextCreationFailed = "SKR2003";

    /// <summary>Page unavailable or closed.</summary>
    public const string PageUnavailableOrClosed = "SKR2004";

    /// <summary>Locator document not found.</summary>
    public const string LocatorDocumentNotFound = "SKR2005";

    /// <summary>Locator ID not found.</summary>
    public const string LocatorIdNotFound = "SKR2006";

    /// <summary>Unsupported locator strategy.</summary>
    public const string UnsupportedLocatorStrategy = "SKR2007";

    /// <summary>Locator not found.</summary>
    public const string LocatorNotFound = "SKR2008";

    /// <summary>Locator cardinality mismatch.</summary>
    public const string LocatorCardinalityMismatch = "SKR2009";

    /// <summary>Locator operation timeout.</summary>
    public const string LocatorOperationTimeout = "SKR2010";

    /// <summary>Navigation rejected by policy.</summary>
    public const string NavigationRejectedByPolicy = "SKR2011";

    /// <summary>Navigation failed.</summary>
    public const string NavigationFailed = "SKR2012";

    /// <summary>Web action failed.</summary>
    public const string WebActionFailed = "SKR2013";

    /// <summary>Web query failed.</summary>
    public const string WebQueryFailed = "SKR2014";

    /// <summary>Screenshot size limit exceeded.</summary>
    public const string ScreenshotSizeLimitExceeded = "SKR2015";

    /// <summary>Browser operation cancelled.</summary>
    public const string BrowserOperationCancelled = "SKR2016";

    /// <summary>Unknown page reference.</summary>
    public const string UnknownPageReference = "SKR2020";

    /// <summary>Page is already closed or cannot be closed.</summary>
    public const string PageAlreadyClosed = "SKR2021";

    /// <summary>Popup did not appear before timeout.</summary>
    public const string PopupTimeout = "SKR2022";

    /// <summary>Frame reference was not found.</summary>
    public const string FrameNotFound = "SKR2023";

    /// <summary>Frame locator cardinality was not satisfied.</summary>
    public const string FrameCardinalityMismatch = "SKR2024";

    /// <summary>Artifact store or artifact reference was unavailable.</summary>
    public const string ArtifactUnavailable = "SKR2025";

    /// <summary>Unknown dialog reference.</summary>
    public const string UnknownDialogReference = "SKR2031";

    /// <summary>Upload failed or source artifact was unavailable.</summary>
    public const string UploadFailed = "SKR2026";

    /// <summary>Download did not begin before timeout.</summary>
    public const string DownloadTimeout = "SKR2027";

    /// <summary>Download exceeded the configured maximum size.</summary>
    public const string DownloadSizeLimitExceeded = "SKR2028";

    /// <summary>Download failed or could not be persisted.</summary>
    public const string DownloadPersistenceFailed = "SKR2029";

    /// <summary>Dialog did not appear before timeout.</summary>
    public const string DialogTimeout = "SKR2030";

    /// <summary>Dialog response was invalid or the dialog was already handled.</summary>
    public const string InvalidDialogResponse = "SKR2032";

    /// <summary>Cookie operation failed.</summary>
    public const string CookieOperationFailed = "SKR2033";

    /// <summary>Storage-state import failed.</summary>
    public const string StorageStateImportFailed = "SKR2034";

    /// <summary>Storage-state export failed.</summary>
    public const string StorageStateExportFailed = "SKR2035";

    /// <summary>Reference belongs to a previous browser context generation.</summary>
    public const string StaleBrowsingContextReference = "SKR2036";

    /// <summary>Browser installation is missing.</summary>
    public const string BrowserInstallationMissing = "SKR2037";

    /// <summary>Advanced wait failed or timed out.</summary>
    public const string AdvancedWaitFailed = "SKR2038";
}
