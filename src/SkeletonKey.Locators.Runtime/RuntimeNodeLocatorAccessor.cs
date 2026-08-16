using System.Collections.ObjectModel;
using SkeletonKey.Locators;

namespace SkeletonKey.Locators.Runtime;

/// <summary>
/// Provides immutable runtime node access to resolved locator bindings.
/// </summary>
public sealed class RuntimeNodeLocatorAccessor : INodeLocatorAccessor
{
    private readonly IReadOnlyDictionary<string, NodeLocatorBinding> _bindings;

    /// <summary>
    /// Initializes a runtime node locator accessor.
    /// </summary>
    /// <param name="bindings">Resolved locator bindings keyed by declared slot name.</param>
    public RuntimeNodeLocatorAccessor(IReadOnlyList<NodeLocatorBinding>? bindings = null)
    {
        _bindings = (bindings ?? Array.AsReadOnly(Array.Empty<NodeLocatorBinding>()))
            .ToDictionary(static binding => binding.SlotName, StringComparer.Ordinal);
    }

    /// <summary>Gets an empty node locator accessor.</summary>
    public static RuntimeNodeLocatorAccessor Empty { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<NodeLocatorBinding> Bindings => new ReadOnlyCollection<NodeLocatorBinding>([.. _bindings.Values.OrderBy(static binding => binding.SlotName, StringComparer.Ordinal)]);

    /// <inheritdoc />
    public bool TryGet(string slotName, out ResolvedLocatorPlan? locator)
    {
        if (_bindings.TryGetValue(slotName, out NodeLocatorBinding? binding))
        {
            locator = binding.Locator;
            return true;
        }

        locator = null;
        return false;
    }
}
