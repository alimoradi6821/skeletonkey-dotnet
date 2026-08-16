using SkeletonKey.Catalog;
using SkeletonKey.Locators;

namespace SkeletonKey.Planning;

/// <summary>
/// Describes one locator slot use planned for a workflow execution step.
/// </summary>
public sealed class NodeLocatorUse
{
    /// <summary>
    /// Initializes planned locator use metadata.
    /// </summary>
    public NodeLocatorUse(string stepId, string nodeId, string slotName, LocatorReference reference, ResolvedLocatorPlan? resolvedLocator, LocatorUsageMode usage, bool required)
    {
        StepId = stepId;
        NodeId = nodeId;
        SlotName = slotName;
        Reference = reference;
        ResolvedLocator = resolvedLocator;
        Usage = usage;
        Required = required;
    }

    /// <summary>Gets the stable plan step identifier.</summary>
    public string StepId { get; }

    /// <summary>Gets the workflow node identifier.</summary>
    public string NodeId { get; }

    /// <summary>Gets the declared locator slot name.</summary>
    public string SlotName { get; }

    /// <summary>Gets the parsed locator reference.</summary>
    public LocatorReference Reference { get; }

    /// <summary>Gets the resolved locator plan, when analysis had repository context.</summary>
    public ResolvedLocatorPlan? ResolvedLocator { get; }

    /// <summary>Gets the declared locator usage mode.</summary>
    public LocatorUsageMode Usage { get; }

    /// <summary>Gets a value indicating whether the slot is required.</summary>
    public bool Required { get; }
}
