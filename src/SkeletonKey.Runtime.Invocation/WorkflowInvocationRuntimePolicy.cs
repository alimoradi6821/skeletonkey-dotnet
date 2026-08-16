using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Invocation;

namespace SkeletonKey.Runtime.Invocation;

/// <summary>
/// Describes materialized invocation behavior for a <c>workflow.invoke</c> activation.
/// </summary>
public sealed class WorkflowInvocationRuntimePolicy
{
    private readonly JsonObject? _inputs;
    private readonly JsonObject? _resources;

    /// <summary>
    /// Initializes a workflow invocation runtime policy.
    /// </summary>
    public WorkflowInvocationRuntimePolicy(
        JsonObject? inputs = null,
        JsonObject? resources = null,
        WorkflowInvocationStreamPolicy? streamPolicy = null)
    {
        _inputs = inputs is null ? null : (JsonObject)inputs.DeepClone();
        _resources = resources is null ? null : (JsonObject)resources.DeepClone();
        StreamPolicy = streamPolicy ?? new WorkflowInvocationStreamPolicy();
    }

    /// <summary>Gets the materialized child input map.</summary>
    public JsonObject? Inputs => _inputs is null ? null : (JsonObject)_inputs.DeepClone();

    /// <summary>Gets the materialized child resource map.</summary>
    public JsonObject? Resources => _resources is null ? null : (JsonObject)_resources.DeepClone();

    /// <summary>Gets the child stream visibility policy.</summary>
    public WorkflowInvocationStreamPolicy StreamPolicy { get; }
}
