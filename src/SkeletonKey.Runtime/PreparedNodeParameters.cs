using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Execution;
using SkeletonKey.Locators;

namespace SkeletonKey.Runtime;

/// <summary>
/// Contains handler-ready parameters and scoped resource and locator bindings prepared by the runtime.
/// </summary>
public sealed class PreparedNodeParameters
{
    private readonly JsonObject _materializedParameters;
    private readonly IReadOnlyList<NodeResourceBinding> _resourceBindings;
    private readonly IReadOnlyList<NodeLocatorBinding> _locatorBindings;

    /// <summary>
    /// Initializes prepared node parameters.
    /// </summary>
    public PreparedNodeParameters(
        JsonObject materializedParameters,
        IReadOnlyList<NodeResourceBinding>? resourceBindings = null,
        IReadOnlyList<NodeLocatorBinding>? locatorBindings = null)
    {
        _materializedParameters = (JsonObject)(materializedParameters ?? throw new ArgumentNullException(nameof(materializedParameters))).DeepClone();
        _resourceBindings = resourceBindings is null ? Array.AsReadOnly(Array.Empty<NodeResourceBinding>()) : new ReadOnlyCollection<NodeResourceBinding>([.. resourceBindings]);
        _locatorBindings = locatorBindings is null ? Array.AsReadOnly(Array.Empty<NodeLocatorBinding>()) : new ReadOnlyCollection<NodeLocatorBinding>([.. locatorBindings]);
    }

    /// <summary>Gets materialized JSON parameters with consumed `$resource` and `$locator` wrappers omitted.</summary>
    public JsonObject MaterializedParameters => (JsonObject)_materializedParameters.DeepClone();

    /// <summary>Gets prepared resource bindings.</summary>
    public IReadOnlyList<NodeResourceBinding> ResourceBindings => new ReadOnlyCollection<NodeResourceBinding>([.. _resourceBindings]);

    /// <summary>Gets prepared locator bindings.</summary>
    public IReadOnlyList<NodeLocatorBinding> LocatorBindings => new ReadOnlyCollection<NodeLocatorBinding>([.. _locatorBindings]);
}
