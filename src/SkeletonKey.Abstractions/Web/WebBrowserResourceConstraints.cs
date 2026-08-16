namespace SkeletonKey.Abstractions.Web;

/// <summary>
/// Describes provider-neutral browser preferences without storing launch options or host paths.
/// </summary>
public sealed class WebBrowserResourceConstraints
{
    /// <summary>
    /// Initializes browser resource constraints.
    /// </summary>
    /// <param name="engine">The preferred browser engine.</param>
    /// <param name="profile">The preferred profile persistence behavior.</param>
    /// <param name="visibility">The preferred browser visibility behavior.</param>
    public WebBrowserResourceConstraints(
        WebBrowserEnginePreference engine = WebBrowserEnginePreference.Any,
        WebBrowserProfilePreference profile = WebBrowserProfilePreference.Any,
        WebBrowserVisibilityPreference visibility = WebBrowserVisibilityPreference.Any)
    {
        Engine = engine;
        Profile = profile;
        Visibility = visibility;
    }

    /// <summary>Gets the provider-neutral browser engine preference.</summary>
    public WebBrowserEnginePreference Engine { get; }

    /// <summary>Gets the provider-neutral profile persistence preference.</summary>
    public WebBrowserProfilePreference Profile { get; }

    /// <summary>Gets the provider-neutral browser visibility preference.</summary>
    public WebBrowserVisibilityPreference Visibility { get; }
}
