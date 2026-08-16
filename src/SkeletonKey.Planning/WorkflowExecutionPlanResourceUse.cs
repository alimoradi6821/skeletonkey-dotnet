using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Planning;

/// <summary>
/// Describes a planned node's use of a workflow resource.
/// </summary>
public sealed class WorkflowExecutionPlanResourceUse
{
    /// <summary>
    /// Initializes a planned resource use.
    /// </summary>
    /// <param name="resourceName">The workflow resource name.</param>
    /// <param name="slotName">The catalog node-local resource slot name.</param>
    /// <param name="access">The effective access mode requested for the resource.</param>
    /// <param name="kind">Optional accepted or resolved resource kind.</param>
    /// <param name="required">Whether the use is required for the planned step.</param>
    /// <param name="capabilities">Required capabilities for this planned use.</param>
    public WorkflowExecutionPlanResourceUse(
        string resourceName,
        string slotName,
        WorkflowResourceAccessMode access,
        string? kind = null,
        bool required = true,
        IReadOnlyList<string>? capabilities = null)
    {
        ResourceName = resourceName;
        SlotName = slotName;
        Access = access;
        Kind = kind;
        Required = required;
        Capabilities = capabilities is null ? Array.AsReadOnly(Array.Empty<string>()) : Array.AsReadOnly([.. capabilities]);
    }

    /// <summary>
    /// Gets the workflow resource name.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets the catalog node-local resource slot name.
    /// </summary>
    public string SlotName { get; }

    /// <summary>
    /// Gets the effective access mode requested for the resource.
    /// </summary>
    public WorkflowResourceAccessMode Access { get; }

    /// <summary>
    /// Gets the optional accepted or resolved resource kind.
    /// </summary>
    public string? Kind { get; }

    /// <summary>
    /// Gets a value indicating whether the use is required for the planned step.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets required capabilities for this planned use.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }
}
