using SkeletonKey.Catalog;

namespace SkeletonKey.Analysis;

/// <summary>
/// Represents catalog analysis for one workflow node instance.
/// </summary>
public sealed class WorkflowNodeAnalysis
{
    private static readonly IReadOnlyList<WorkflowAnalysisIssue> _emptyIssues = Array.AsReadOnly(Array.Empty<WorkflowAnalysisIssue>());
    private static readonly IReadOnlyList<WorkflowEffectivePort> _emptyPorts = Array.AsReadOnly(Array.Empty<WorkflowEffectivePort>());
    private static readonly IReadOnlyList<WorkflowResourceSlotAnalysis> _emptyResources = Array.AsReadOnly(Array.Empty<WorkflowResourceSlotAnalysis>());
    private static readonly IReadOnlyList<WorkflowLocatorSlotAnalysis> _emptyLocators = Array.AsReadOnly(Array.Empty<WorkflowLocatorSlotAnalysis>());

    /// <summary>
    /// Initializes node analysis.
    /// </summary>
    /// <param name="nodeId">The workflow node identifier.</param>
    /// <param name="nodeType">The workflow node type identifier.</param>
    /// <param name="typeVersion">The workflow node type version.</param>
    /// <param name="disabled">Whether the node is disabled in the workflow document.</param>
    /// <param name="catalogStatus">Whether the node matched catalog metadata.</param>
    /// <param name="definition">The exact catalog definition, when available.</param>
    /// <param name="parameterStatus">Catalog parameter contract analysis status.</param>
    /// <param name="resourceStatus">Catalog resource requirement analysis status.</param>
    /// <param name="capabilityStatus">Catalog capability compatibility status.</param>
    /// <param name="issues">Node-specific issues in deterministic order.</param>
    /// <param name="effectivePorts">Resolved effective static and dynamic ports in deterministic order.</param>
    /// <param name="resourceSlots">Resolved node resource-slot analysis records in deterministic order.</param>
    /// <param name="locatorSlots">Resolved node locator-slot analysis records in deterministic order.</param>
    public WorkflowNodeAnalysis(
        string nodeId,
        string nodeType,
        int typeVersion,
        bool disabled,
        WorkflowNodeCatalogStatus catalogStatus,
        WorkflowNodeDefinition? definition = null,
        WorkflowParameterAnalysisStatus parameterStatus = WorkflowParameterAnalysisStatus.NotAnalyzed,
        WorkflowResourceRequirementAnalysisStatus resourceStatus = WorkflowResourceRequirementAnalysisStatus.NotAnalyzed,
        WorkflowCapabilityCompatibilityStatus capabilityStatus = WorkflowCapabilityCompatibilityStatus.NotAnalyzed,
        IReadOnlyList<WorkflowAnalysisIssue>? issues = null,
        IReadOnlyList<WorkflowEffectivePort>? effectivePorts = null,
        IReadOnlyList<WorkflowResourceSlotAnalysis>? resourceSlots = null,
        IReadOnlyList<WorkflowLocatorSlotAnalysis>? locatorSlots = null)
    {
        NodeId = nodeId;
        NodeType = nodeType;
        TypeVersion = typeVersion;
        Disabled = disabled;
        CatalogStatus = catalogStatus;
        Definition = definition;
        DefinitionKey = definition?.Key;
        ParameterStatus = parameterStatus;
        ResourceStatus = resourceStatus;
        CapabilityStatus = capabilityStatus;
        Issues = issues is null ? _emptyIssues : Array.AsReadOnly([.. issues]);
        EffectivePorts = effectivePorts is null ? _emptyPorts : Array.AsReadOnly([.. effectivePorts]);
        ResourceSlots = resourceSlots is null ? _emptyResources : Array.AsReadOnly([.. resourceSlots]);
        LocatorSlots = locatorSlots is null ? _emptyLocators : Array.AsReadOnly([.. locatorSlots]);
    }

    /// <summary>
    /// Gets the workflow node identifier.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the workflow node type identifier.
    /// </summary>
    public string NodeType { get; }

    /// <summary>
    /// Gets the workflow node type version.
    /// </summary>
    public int TypeVersion { get; }

    /// <summary>
    /// Gets a value indicating whether the node is disabled in the workflow document.
    /// </summary>
    public bool Disabled { get; }

    /// <summary>
    /// Gets whether the node matched catalog metadata.
    /// </summary>
    public WorkflowNodeCatalogStatus CatalogStatus { get; }

    /// <summary>
    /// Gets the exact catalog definition, when available.
    /// </summary>
    public WorkflowNodeDefinition? Definition { get; }

    /// <summary>
    /// Gets the resolved node definition key, when available.
    /// </summary>
    public WorkflowNodeDefinitionKey? DefinitionKey { get; }

    /// <summary>
    /// Gets catalog parameter contract analysis status.
    /// </summary>
    public WorkflowParameterAnalysisStatus ParameterStatus { get; }

    /// <summary>
    /// Gets catalog resource requirement analysis status.
    /// </summary>
    public WorkflowResourceRequirementAnalysisStatus ResourceStatus { get; }

    /// <summary>
    /// Gets catalog capability compatibility status.
    /// </summary>
    public WorkflowCapabilityCompatibilityStatus CapabilityStatus { get; }

    /// <summary>
    /// Gets resolved effective static and dynamic ports in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowEffectivePort> EffectivePorts { get; }

    /// <summary>
    /// Gets resolved node resource-slot analysis records in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowResourceSlotAnalysis> ResourceSlots { get; }

    /// <summary>
    /// Gets resolved node locator-slot analysis records in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowLocatorSlotAnalysis> LocatorSlots { get; }

    /// <summary>
    /// Gets node-specific issues in deterministic order.
    /// </summary>
    public IReadOnlyList<WorkflowAnalysisIssue> Issues { get; }
}
