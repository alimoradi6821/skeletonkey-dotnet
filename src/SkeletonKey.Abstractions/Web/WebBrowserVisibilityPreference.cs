namespace SkeletonKey.Abstractions.Web;

/// <summary>
/// Describes a provider-neutral browser visibility preference.
/// </summary>
public enum WebBrowserVisibilityPreference
{
    /// <summary>Any browser visibility behavior is acceptable.</summary>
    Any,

    /// <summary>A headless browser is preferred or required.</summary>
    Headless,

    /// <summary>A visible browser is preferred or required.</summary>
    Headful,
}
