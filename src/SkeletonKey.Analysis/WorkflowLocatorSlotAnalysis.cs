using SkeletonKey.Catalog;
using SkeletonKey.Locators;

namespace SkeletonKey.Analysis;

/// <summary>
/// Describes immutable analysis metadata for one declared node locator slot.
/// </summary>
public sealed class WorkflowLocatorSlotAnalysis
{
    /// <summary>
    /// Initializes locator-slot analysis metadata.
    /// </summary>
    public WorkflowLocatorSlotAnalysis(
        string slotName,
        LocatorReference? reference,
        ResolvedLocatorPlan? resolvedLocator,
        bool required,
        LocatorUsageMode usage,
        IReadOnlyList<LocatorCardinality>? acceptedCardinalities,
        WorkflowLocatorSlotAnalysisStatus status,
        string parameterPath)
    {
        SlotName = slotName;
        Reference = reference;
        ResolvedLocator = resolvedLocator;
        Required = required;
        Usage = usage;
        AcceptedCardinalities = acceptedCardinalities is null ? Array.AsReadOnly(Array.Empty<LocatorCardinality>()) : Array.AsReadOnly([.. acceptedCardinalities]);
        Status = status;
        ParameterPath = parameterPath;
    }

    /// <summary>Gets the node-local catalog locator slot name.</summary>
    public string SlotName { get; }

    /// <summary>Gets the locator reference, when statically parsed.</summary>
    public LocatorReference? Reference { get; }

    /// <summary>Gets the resolved locator plan, when repository context was available.</summary>
    public ResolvedLocatorPlan? ResolvedLocator { get; }

    /// <summary>Gets a value indicating whether the catalog slot is required.</summary>
    public bool Required { get; }

    /// <summary>Gets the declared locator usage mode.</summary>
    public LocatorUsageMode Usage { get; }

    /// <summary>Gets accepted resolved locator cardinalities.</summary>
    public IReadOnlyList<LocatorCardinality> AcceptedCardinalities { get; }

    /// <summary>Gets the locator-slot analysis status.</summary>
    public WorkflowLocatorSlotAnalysisStatus Status { get; }

    /// <summary>Gets the JSON Pointer to the node parameter slot.</summary>
    public string ParameterPath { get; }
}
