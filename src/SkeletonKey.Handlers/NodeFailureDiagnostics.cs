using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;

namespace SkeletonKey.Handlers;

/// <summary>Represents bounded provider-neutral evidence captured after a node failure.</summary>
public sealed class NodeFailureDiagnosticContribution
{
    private readonly JsonObject _data;

    /// <summary>Initializes one diagnostic contribution.</summary>
    public NodeFailureDiagnosticContribution(string contributorId, JsonObject data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributorId);
        ArgumentNullException.ThrowIfNull(data);
        ContributorId = contributorId;
        _data = (JsonObject)data.DeepClone();
    }

    /// <summary>Gets the stable contributor identifier.</summary>
    public string ContributorId { get; }

    /// <summary>Gets a defensive copy of the evidence payload.</summary>
    public JsonObject Data => (JsonObject)_data.DeepClone();
}

/// <summary>Allows providers to attach bounded evidence to a failed node without changing execution semantics.</summary>
public interface INodeFailureDiagnosticContributor
{
    /// <summary>Gets the stable contributor identifier.</summary>
    public string Id { get; }

    /// <summary>Reports whether this contributor applies to one exact node definition.</summary>
    public bool AppliesTo(WorkflowNodeDefinitionKey definition);

    /// <summary>Captures evidence for a failed node. Implementations must not throw intentionally.</summary>
    public ValueTask<NodeFailureDiagnosticContribution?> CaptureAsync(
        NodeExecutionIdentity identity,
        WorkflowError error,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default);
}
