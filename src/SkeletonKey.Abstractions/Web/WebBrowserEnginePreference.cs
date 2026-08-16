namespace SkeletonKey.Abstractions.Web;

/// <summary>
/// Describes a provider-neutral browser engine preference.
/// </summary>
public enum WebBrowserEnginePreference
{
    /// <summary>Any browser engine is acceptable.</summary>
    Any,

    /// <summary>Chromium is preferred or required by the workflow contract.</summary>
    Chromium,

    /// <summary>Firefox is preferred or required by the workflow contract.</summary>
    Firefox,

    /// <summary>WebKit is preferred or required by the workflow contract.</summary>
    WebKit,
}
