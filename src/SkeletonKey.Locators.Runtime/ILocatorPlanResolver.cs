using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Resolves locator references into browser-free locator plans.
/// </summary>
public interface ILocatorPlanResolver
{
    /// <summary>
    /// Resolves a locator reference by exact catalog identity.
    /// </summary>
    public ValueTask<ResolvedLocatorPlan> ResolveAsync(LocatorReference reference, CancellationToken cancellationToken = default);
}
