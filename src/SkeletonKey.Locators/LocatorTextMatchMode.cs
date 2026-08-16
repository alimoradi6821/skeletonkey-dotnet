namespace SkeletonKey.Locators;

/// <summary>
/// Describes semantic text matching for locator strategies.
/// </summary>
public enum LocatorTextMatchMode
{
    /// <summary>The candidate text must exactly match.</summary>
    Exact,

    /// <summary>The candidate text may contain the supplied value.</summary>
    Contains,
}
