using SkeletonKey.Catalog;

namespace SkeletonKey.Handlers;

/// <summary>
/// Defines exact node handler lookup by versioned node definition identity.
/// </summary>
/// <remarks>
/// Resolution is ordinal, case-sensitive, and has no implicit latest-version behavior. This interface does not define registration,
/// dependency injection adapters, assembly scanning, plugin loading, or a global singleton.
/// </remarks>
public interface INodeHandlerResolver
{
    /// <summary>
    /// Attempts to resolve a handler for one exact node definition identity.
    /// </summary>
    /// <param name="definition">The exact node definition identity.</param>
    /// <param name="handler">When successful, receives the matching node handler.</param>
    /// <returns><see langword="true" /> when an exact handler is available; otherwise, <see langword="false" />.</returns>
    public bool TryResolve(
        WorkflowNodeDefinitionKey definition,
        out INodeHandler? handler);
}
