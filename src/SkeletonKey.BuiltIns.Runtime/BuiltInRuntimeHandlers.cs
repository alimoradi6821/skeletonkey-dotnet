using SkeletonKey.Abstractions.Interaction;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Creates immutable handler collections for executable built-in node definitions.
/// </summary>
/// <remarks>
/// The default collection includes start, end, return, branch, and loop handlers. The interaction handler is included only when a host supplies an
/// explicit <see cref="IWorkflowInteractionHandler" />. Workflow invocation remains runtime-owned and intentionally has no ordinary node handler.
/// </remarks>
public static class BuiltInRuntimeHandlers
{
    /// <summary>
    /// Creates the built-in handler set.
    /// </summary>
    /// <param name="interactionHandler">Optional host interaction boundary for non-durable interaction requests.</param>
    /// <returns>Handlers in stable exact-definition order.</returns>
    public static IReadOnlyList<INodeHandler> Create(IWorkflowInteractionHandler? interactionHandler = null)
    {
        List<INodeHandler> handlers =
        [
            new CoreStartHandler(),
            new CoreEndHandler(),
            new CoreReturnHandler(),
            new FlowIfHandler(),
            new FlowForEachHandler(),
            new FlowRepeatHandler(),
            new FlowSwitchHandler(),
            new FlowWhileHandler(),
        ];

        if (interactionHandler is not null)
        {
            handlers.Add(new InteractionRequestHandler(interactionHandler));
        }

        return Array.AsReadOnly([.. handlers.OrderBy(static handler => handler.Definition.Type, StringComparer.Ordinal).ThenBy(static handler => handler.Definition.Version)]);
    }

    /// <summary>
    /// Creates an immutable exact resolver for the built-in handler set.
    /// </summary>
    /// <param name="interactionHandler">Optional host interaction boundary for non-durable interaction requests.</param>
    /// <returns>An immutable exact node-handler resolver.</returns>
    public static ImmutableNodeHandlerResolver CreateResolver(IWorkflowInteractionHandler? interactionHandler = null)
    {
        return new ImmutableNodeHandlerResolver(Create(interactionHandler));
    }
}
