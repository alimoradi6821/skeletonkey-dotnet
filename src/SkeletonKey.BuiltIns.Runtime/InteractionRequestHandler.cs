using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Abstractions.Execution;
using SkeletonKey.Abstractions.Interaction;
using SkeletonKey.Catalog;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Executes the exact <c>interaction.request</c> built-in node contract through a supplied host interaction handler.
/// </summary>
/// <remarks>
/// This handler supports non-durable asynchronous interaction only. It does not persist, resume, access files, network, browser automation, time, or randomness.
/// </remarks>
public sealed class InteractionRequestHandler : INodeHandler
{
    private readonly IWorkflowInteractionHandler _interactionHandler;

    /// <summary>
    /// Initializes a new interaction request handler.
    /// </summary>
    /// <param name="interactionHandler">The host-neutral interaction handler to call.</param>
    public InteractionRequestHandler(IWorkflowInteractionHandler interactionHandler)
    {
        _interactionHandler = interactionHandler ?? throw new ArgumentNullException(nameof(interactionHandler));
    }

    /// <inheritdoc />
    public WorkflowNodeDefinitionKey Definition { get; } = new("interaction.request", 1);

    /// <inheritdoc />
    public async ValueTask<NodeHandlerResult> ExecuteAsync(NodeExecutionRequest request, INodeExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        WorkflowInteractionRequest interactionRequest = BuildRequest(request);
        WorkflowInteractionResponse response = await _interactionHandler.RequestAsync(interactionRequest, cancellationToken).ConfigureAwait(false);
        JsonObject result = new()
        {
            ["requestId"] = response.RequestId,
            ["status"] = response.Status.ToString(),
            ["hasValue"] = response.HasValue,
        };
        if (response.HasValue)
        {
            result["value"] = response.Value;
        }

        return NodeHandlerResult.Success(new NodeHandlerOutputs(
            ["result"],
            new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
            {
                ["result"] = new([result]),
            }));
    }

    private static WorkflowInteractionRequest BuildRequest(NodeExecutionRequest request)
    {
        JsonObject parameters = request.Parameters;
        if (parameters["kind"] is null || parameters["prompt"] is null || parameters["prompt"]!.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidOperationException("interaction.request requires materialized kind and prompt parameters.");
        }

        WorkflowInteractionKind kind = parameters["kind"]!.GetValue<string>() switch
        {
            "confirmation" => WorkflowInteractionKind.Confirmation,
            "choice" => WorkflowInteractionKind.Choice,
            "manual-action" => WorkflowInteractionKind.ManualAction,
            "secret" => WorkflowInteractionKind.Secret,
            _ => throw new InvalidOperationException("interaction.request kind is not supported."),
        };

        return new WorkflowInteractionRequest(
            $"interaction:{request.Identity.ExecutionId}:{request.Identity.NodeId}:{request.Identity.Attempt}",
            request.Identity.ExecutionId,
            request.Identity.InvocationId,
            request.Identity.WorkflowId,
            request.Identity.NodeId,
            kind,
            parameters["prompt"]!.GetValue<string>());
    }
}
