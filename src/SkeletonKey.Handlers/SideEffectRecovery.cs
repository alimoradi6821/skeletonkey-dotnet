using SkeletonKey.Execution;

namespace SkeletonKey.Handlers;

/// <summary>Marks a handler whose execution may cause an externally observable side effect.</summary>
public interface IExternalSideEffectNodeHandler : INodeHandler
{
}

/// <summary>Classifies reconciliation of a previously uncertain external side effect.</summary>
public enum NodeSideEffectRecoveryStatus
{
    /// <summary>The external operation was definitely not attempted and normal execution may replay it.</summary>
    NotAttempted,

    /// <summary>The external operation completed and the supplied outputs may be committed without replay.</summary>
    Completed,

    /// <summary>The external outcome remains uncertain and must not be replayed automatically.</summary>
    Uncertain,
}

/// <summary>Represents one explicit side-effect reconciliation result.</summary>
public sealed class NodeSideEffectRecoveryResult
{
    private NodeSideEffectRecoveryResult(NodeSideEffectRecoveryStatus status, NodeHandlerOutputs? outputs)
    {
        Status = status;
        Outputs = outputs;
    }

    /// <summary>Gets the reconciliation classification.</summary>
    public NodeSideEffectRecoveryStatus Status { get; }

    /// <summary>Gets recovered outputs when <see cref="Status" /> is <see cref="NodeSideEffectRecoveryStatus.Completed" />.</summary>
    public NodeHandlerOutputs? Outputs { get; }

    /// <summary>Reports that dispatch definitely did not occur and execution may safely replay.</summary>
    public static NodeSideEffectRecoveryResult NotAttempted() => new(NodeSideEffectRecoveryStatus.NotAttempted, null);

    /// <summary>Reports that the external operation completed with recovered outputs.</summary>
    public static NodeSideEffectRecoveryResult Completed(NodeHandlerOutputs outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return new NodeSideEffectRecoveryResult(NodeSideEffectRecoveryStatus.Completed, outputs);
    }

    /// <summary>Reports that the outcome cannot be determined safely.</summary>
    public static NodeSideEffectRecoveryResult Uncertain() => new(NodeSideEffectRecoveryStatus.Uncertain, null);
}

/// <summary>Reconciles an external side effect whose dispatch was durably recorded as uncertain.</summary>
public interface INodeSideEffectRecoveryHandler : IExternalSideEffectNodeHandler
{
    /// <summary>Determines whether the prior external operation ran, completed, or remains uncertain.</summary>
    public ValueTask<NodeSideEffectRecoveryResult> ReconcileAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default);
}
