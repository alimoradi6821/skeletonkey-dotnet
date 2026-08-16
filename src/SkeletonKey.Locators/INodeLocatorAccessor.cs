namespace SkeletonKey.Locators;

/// <summary>
/// Exposes resolved locator bindings by declared node locator slot only.
/// </summary>
public interface INodeLocatorAccessor
{
    /// <summary>Gets immutable locator bindings visible to the node handler.</summary>
    public IReadOnlyList<NodeLocatorBinding> Bindings { get; }

    /// <summary>
    /// Attempts to get one resolved locator plan by declared slot name.
    /// </summary>
    /// <param name="slotName">The declared node locator slot name.</param>
    /// <param name="locator">When successful, receives the resolved locator plan.</param>
    /// <returns><see langword="true" /> when the slot is bound; otherwise, <see langword="false" />.</returns>
    public bool TryGet(string slotName, out ResolvedLocatorPlan? locator);
}
