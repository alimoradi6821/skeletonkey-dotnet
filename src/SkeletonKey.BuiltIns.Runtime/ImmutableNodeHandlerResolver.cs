using System.Collections.ObjectModel;
using SkeletonKey.Catalog;
using SkeletonKey.Handlers;

namespace SkeletonKey.BuiltIns.Runtime;

/// <summary>
/// Provides immutable exact node-handler resolution by versioned node definition identity.
/// </summary>
/// <remarks>
/// The resolver has no mutation, no global singleton, no dependency-injection registration, no assembly scanning, and no implicit latest-version fallback.
/// </remarks>
public sealed class ImmutableNodeHandlerResolver : INodeHandlerResolver
{
    private readonly IReadOnlyDictionary<WorkflowNodeDefinitionKey, INodeHandler> _handlers;

    /// <summary>
    /// Initializes a new immutable exact handler resolver.
    /// </summary>
    /// <param name="handlers">The handlers to expose for exact lookup.</param>
    /// <exception cref="ArgumentException">Thrown when duplicate exact handler definitions are supplied.</exception>
    public ImmutableNodeHandlerResolver(IEnumerable<INodeHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        Dictionary<WorkflowNodeDefinitionKey, INodeHandler> map = new();
        foreach (INodeHandler handler in handlers)
        {
            if (!map.TryAdd(handler.Definition, handler))
            {
                throw new ArgumentException("Duplicate exact node handler definition.", nameof(handlers));
            }
        }

        _handlers = new ReadOnlyDictionary<WorkflowNodeDefinitionKey, INodeHandler>(map);
    }

    /// <summary>
    /// Gets handlers in stable definition order.
    /// </summary>
    public IReadOnlyList<INodeHandler> Handlers => Array.AsReadOnly([.. _handlers.Values.OrderBy(static handler => handler.Definition.Type, StringComparer.Ordinal).ThenBy(static handler => handler.Definition.Version)]);

    /// <inheritdoc />
    public bool TryResolve(WorkflowNodeDefinitionKey definition, out INodeHandler? handler)
    {
        return _handlers.TryGetValue(definition, out handler);
    }
}
