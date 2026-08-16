using SkeletonKey.Catalog;
using SkeletonKey.Execution;

namespace SkeletonKey.Handlers;

/// <summary>
/// Defines a host-neutral handler for one exact versioned node definition.
/// </summary>
/// <remarks>
/// Handlers receive materialized parameters, explicit input values, scoped resources, runtime-owned observation interfaces, and a cancellation token.
/// Expected node failures should normally return <see cref="NodeHandlerResult" />. Operation-cancelled exceptions associated with the supplied token
/// and unexpected implementation exceptions are normalized by a future runtime. This contract does not execute, scan, or register handlers.
/// </remarks>
public interface INodeHandler
{
    /// <summary>
    /// Gets the exact node definition identity handled by this handler.
    /// </summary>
    public WorkflowNodeDefinitionKey Definition { get; }

    /// <summary>
    /// Executes one node attempt through the handler boundary.
    /// </summary>
    /// <param name="request">The immutable node execution request with materialized parameters and explicit inputs.</param>
    /// <param name="context">The immutable-scoped execution context with event and resource boundaries.</param>
    /// <param name="cancellationToken">The runtime-supplied cancellation token that handlers must honor.</param>
    /// <returns>The lightweight handler result to be converted by a future runtime.</returns>
    public ValueTask<NodeHandlerResult> ExecuteAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        CancellationToken cancellationToken = default);
}
