namespace SkeletonKey.Locators;

/// <summary>
/// Binds one declared node locator slot to a resolved browser-free locator plan.
/// </summary>
public sealed class NodeLocatorBinding
{
    /// <summary>
    /// Initializes a node locator binding.
    /// </summary>
    public NodeLocatorBinding(string slotName, LocatorReference reference, ResolvedLocatorPlan locator, bool required)
    {
        SlotName = slotName;
        Reference = reference;
        Locator = locator;
        Required = required;
    }

    /// <summary>Gets the declared locator slot name.</summary>
    public string SlotName { get; }

    /// <summary>Gets the exact locator reference consumed from node parameters.</summary>
    public LocatorReference Reference { get; }

    /// <summary>Gets the resolved provider-neutral locator plan.</summary>
    public ResolvedLocatorPlan Locator { get; }

    /// <summary>Gets a value indicating whether the slot is required.</summary>
    public bool Required { get; }
}
