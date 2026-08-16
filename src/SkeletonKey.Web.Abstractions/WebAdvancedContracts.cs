using SkeletonKey.Artifacts;
using SkeletonKey.Locators;

namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Represents an opaque page reference scoped to one runtime web resource instance.
/// </summary>
public sealed class WebPageReference
{
    /// <summary>Initializes a runtime-scoped page reference.</summary>
    public WebPageReference(string pageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);
        PageId = pageId;
    }

    /// <summary>Gets the opaque case-sensitive page identifier.</summary>
    public string PageId { get; }
}

/// <summary>
/// Describes provider-neutral metadata for one runtime page.
/// </summary>
public sealed class WebPageInformation
{
    /// <summary>Initializes page metadata.</summary>
    public WebPageInformation(WebPageReference reference, string url, string? title, bool isActive, bool isClosed)
    {
        Reference = reference;
        Url = url;
        Title = title;
        IsActive = isActive;
        IsClosed = isClosed;
    }

    /// <summary>Gets the opaque page reference.</summary>
    public WebPageReference Reference { get; }

    /// <summary>Gets the current page URL when known.</summary>
    public string Url { get; }

    /// <summary>Gets the current page title when known.</summary>
    public string? Title { get; }

    /// <summary>Gets whether this page is the active default page.</summary>
    public bool IsActive { get; }

    /// <summary>Gets whether this page is known but closed.</summary>
    public bool IsClosed { get; }
}

/// <summary>
/// Represents an immutable snapshot of a page collection in creation order.
/// </summary>
public sealed class WebPageCollectionSnapshot
{
    /// <summary>Initializes a page collection snapshot.</summary>
    public WebPageCollectionSnapshot(IReadOnlyList<WebPageInformation> pages)
    {
        Pages = Array.AsReadOnly([.. pages]);
    }

    /// <summary>Gets pages in deterministic creation order.</summary>
    public IReadOnlyList<WebPageInformation> Pages { get; }
}

/// <summary>
/// Describes optional page and frame targeting for an operation.
/// </summary>
public sealed class WebTargetContext
{
    /// <summary>Initializes a target context.</summary>
    public WebTargetContext(WebPageReference? page = null, IReadOnlyList<ResolvedLocatorPlan>? frames = null)
    {
        Page = page;
        Frames = frames is null ? Array.AsReadOnly(Array.Empty<ResolvedLocatorPlan>()) : Array.AsReadOnly([.. frames]);
    }

    /// <summary>Gets the optional page reference; null means active page.</summary>
    public WebPageReference? Page { get; }

    /// <summary>Gets outer-to-inner frame locator plans.</summary>
    public IReadOnlyList<ResolvedLocatorPlan> Frames { get; }
}

/// <summary>
/// Describes a dialog kind without exposing browser provider types.
/// </summary>
public enum WebDialogKind
{
    /// <summary>An alert dialog.</summary>
    Alert,

    /// <summary>A confirmation dialog.</summary>
    Confirm,

    /// <summary>A prompt dialog.</summary>
    Prompt,

    /// <summary>A before-unload dialog.</summary>
    BeforeUnload,
}

/// <summary>
/// Represents an opaque runtime-scoped single-use dialog reference.
/// </summary>
public sealed class WebDialogReference
{
    /// <summary>Initializes a dialog reference.</summary>
    public WebDialogReference(string dialogId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialogId);
        DialogId = dialogId;
    }

    /// <summary>Gets the opaque dialog identifier.</summary>
    public string DialogId { get; }
}

/// <summary>
/// Describes provider-neutral dialog metadata.
/// </summary>
public sealed class WebDialogInformation
{
    /// <summary>Initializes dialog metadata.</summary>
    public WebDialogInformation(WebDialogReference reference, WebDialogKind kind, string message)
    {
        Reference = reference;
        Kind = kind;
        Message = message;
    }

    /// <summary>Gets the opaque dialog reference.</summary>
    public WebDialogReference Reference { get; }

    /// <summary>Gets the dialog kind.</summary>
    public WebDialogKind Kind { get; }

    /// <summary>Gets the dialog message.</summary>
    public string Message { get; }
}

/// <summary>
/// Represents immutable provider-neutral cookie data. Values are sensitive by default.
/// </summary>
public sealed class WebCookie
{
    /// <summary>Initializes cookie data.</summary>
    public WebCookie(string name, string value, string domain, string path = "/", double? expires = null, string sameSite = "Lax", bool httpOnly = false, bool secure = false)
    {
        Name = name;
        Value = value;
        Domain = domain;
        Path = path;
        Expires = expires;
        SameSite = sameSite;
        HttpOnly = httpOnly;
        Secure = secure;
    }

    /// <summary>Gets the cookie name.</summary>
    public string Name { get; }

    /// <summary>Gets the sensitive cookie value.</summary>
    public string Value { get; }

    /// <summary>Gets the cookie domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the cookie path.</summary>
    public string Path { get; }

    /// <summary>Gets the optional expiration timestamp in Unix seconds.</summary>
    public double? Expires { get; }

    /// <summary>Gets the strict SameSite value.</summary>
    public string SameSite { get; }

    /// <summary>Gets whether the cookie is HTTP-only.</summary>
    public bool HttpOnly { get; }

    /// <summary>Gets whether the cookie is secure.</summary>
    public bool Secure { get; }
}

/// <summary>
/// Contains request data for an upload operation.
/// </summary>
public sealed record WebUploadFilesRequest(WebTargetContext TargetContext, IReadOnlyList<WorkflowArtifactReference> Artifacts, int TimeoutMilliseconds = 30000, int? ElementIndex = null, int MaximumFiles = 16, long MaximumAggregateBytes = 64 * 1024 * 1024);

/// <summary>
/// Contains request data for a download-producing click operation.
/// </summary>
public sealed record WebDownloadRequest(WebTargetContext TargetContext, int TimeoutMilliseconds = 30000, long MaximumBytes = 64 * 1024 * 1024, WorkflowArtifactSensitivity Sensitivity = WorkflowArtifactSensitivity.Internal, int? ElementIndex = null);

/// <summary>
/// Contains request data for a popup-producing click operation.
/// </summary>
public sealed record WebPopupRequest(WebTargetContext TargetContext, string Button = "left", int ClickCount = 1, int TimeoutMilliseconds = 30000, bool ActivatePopup = true, int? ElementIndex = null);

/// <summary>
/// Contains request data for a dialog-producing click operation.
/// </summary>
public sealed record WebDialogWaitRequest(WebTargetContext TargetContext, string Button = "left", int ClickCount = 1, int TimeoutMilliseconds = 30000, int? ElementIndex = null);

/// <summary>
/// Contains request data for storage-state export/import.
/// </summary>
public sealed record WebStorageStateRequest(long MaximumBytes = 4 * 1024 * 1024);
