namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Provides the default navigation policy allowing http, https, data, and about URLs.
/// </summary>
public sealed class DefaultWebNavigationPolicy : IWebNavigationPolicy
{
    private static readonly HashSet<string> _allowedSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "data", "about" };

    /// <inheritdoc />
    public WebOperationError? ValidateNavigation(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return new WebOperationError(WebAutomationErrorCodes.NavigationRejectedByPolicy, "Navigation URL must be absolute.", "navigate");
        }

        return _allowedSchemes.Contains(uri.Scheme)
            ? null
            : new WebOperationError(WebAutomationErrorCodes.NavigationRejectedByPolicy, "Navigation scheme is rejected by policy.", "navigate");
    }
}
