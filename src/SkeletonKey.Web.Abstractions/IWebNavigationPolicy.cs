namespace SkeletonKey.Web.Abstractions;

/// <summary>
/// Defines a provider-neutral navigation security boundary.
/// </summary>
public interface IWebNavigationPolicy
{
    /// <summary>
    /// Validates a navigation URL before a provider navigates.
    /// </summary>
    /// <param name="url">The materialized URL.</param>
    /// <returns>A structured error when navigation is rejected; otherwise, <see langword="null" />.</returns>
    public WebOperationError? ValidateNavigation(string url);
}
