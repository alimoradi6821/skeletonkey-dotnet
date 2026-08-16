using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Analysis;

/// <summary>
/// Describes an immutable resolved or unresolved node resource-slot analysis result.
/// </summary>
/// <remarks>
/// Resource-slot analysis matches workflow declarations to catalog requirements without resolving,
/// acquiring, locking, or contacting any live resource.
/// </remarks>
public sealed class WorkflowResourceSlotAnalysis
{
    private static readonly IReadOnlyList<string> _emptyCapabilities = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>
    /// Initializes resource-slot analysis metadata.
    /// </summary>
    /// <param name="slotName">The node-local catalog slot name.</param>
    /// <param name="workflowResourceName">The referenced workflow resource name, when statically resolved.</param>
    /// <param name="required">Whether the catalog slot is required.</param>
    /// <param name="requiredKind">The catalog-required resource kind.</param>
    /// <param name="access">The declared workflow resource access mode, when resolved.</param>
    /// <param name="requiredCapabilities">Ordered catalog-required capabilities.</param>
    /// <param name="status">The resource-slot analysis status.</param>
    /// <param name="parameterPath">The JSON Pointer to the node parameter slot.</param>
    public WorkflowResourceSlotAnalysis(
        string slotName,
        string? workflowResourceName,
        bool required,
        string requiredKind,
        WorkflowResourceAccessMode? access,
        IReadOnlyList<string>? requiredCapabilities,
        WorkflowResourceRequirementAnalysisStatus status,
        string parameterPath)
    {
        SlotName = slotName;
        WorkflowResourceName = workflowResourceName;
        Required = required;
        RequiredKind = requiredKind;
        Access = access;
        RequiredCapabilities = requiredCapabilities is null ? _emptyCapabilities : Array.AsReadOnly([.. requiredCapabilities]);
        Status = status;
        ParameterPath = parameterPath;
    }

    /// <summary>Gets the node-local catalog slot name.</summary>
    public string SlotName { get; }

    /// <summary>Gets the referenced workflow resource name, when statically resolved.</summary>
    public string? WorkflowResourceName { get; }

    /// <summary>Gets a value indicating whether the catalog slot is required.</summary>
    public bool Required { get; }

    /// <summary>Gets the catalog-required resource kind.</summary>
    public string RequiredKind { get; }

    /// <summary>Gets the declared workflow resource access mode, when resolved.</summary>
    public WorkflowResourceAccessMode? Access { get; }

    /// <summary>Gets ordered catalog-required capabilities.</summary>
    public IReadOnlyList<string> RequiredCapabilities { get; }

    /// <summary>Gets the resource-slot analysis status.</summary>
    public WorkflowResourceRequirementAnalysisStatus Status { get; }

    /// <summary>Gets the JSON Pointer to the node parameter slot.</summary>
    public string ParameterPath { get; }
}
