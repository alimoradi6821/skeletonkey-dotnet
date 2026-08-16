namespace SkeletonKey.Abstractions.Web;

/// <summary>
/// Describes a provider-neutral browser profile persistence preference.
/// </summary>
public enum WebBrowserProfilePreference
{
    /// <summary>Any profile behavior is acceptable.</summary>
    Any,

    /// <summary>An ephemeral browser profile is preferred or required.</summary>
    Ephemeral,

    /// <summary>A persistent browser profile is preferred or required.</summary>
    Persistent,
}
