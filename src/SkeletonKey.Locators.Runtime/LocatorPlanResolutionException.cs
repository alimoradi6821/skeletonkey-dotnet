namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Represents a deterministic locator plan resolution failure.
/// </summary>
public sealed class LocatorPlanResolutionException : Exception
{
    /// <summary>
    /// Initializes a locator plan resolution exception.
    /// </summary>
    public LocatorPlanResolutionException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Gets the stable provider-neutral error code.</summary>
    public string Code { get; }
}
