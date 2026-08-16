using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace SkeletonKey.Catalog;

/// <summary>
/// Describes one host-neutral node type contract exposed by a catalog.
/// </summary>
public sealed class WorkflowNodeDefinition
{
    private static readonly IReadOnlyDictionary<string, WorkflowPortDefinition> _emptyPorts = new ReadOnlyDictionary<string, WorkflowPortDefinition>(new Dictionary<string, WorkflowPortDefinition>());
    private static readonly IReadOnlyDictionary<string, WorkflowNodeResourceRequirement> _emptyResources = new ReadOnlyDictionary<string, WorkflowNodeResourceRequirement>(new Dictionary<string, WorkflowNodeResourceRequirement>());
    private static readonly IReadOnlyDictionary<string, NodeLocatorSlotDefinition> _emptyLocators = new ReadOnlyDictionary<string, NodeLocatorSlotDefinition>(new Dictionary<string, NodeLocatorSlotDefinition>());
    private static readonly IReadOnlyList<string> _emptyCapabilities = Array.AsReadOnly(Array.Empty<string>());
    private static readonly IReadOnlyList<WorkflowDynamicPortRule> _emptyDynamicPortRules = Array.AsReadOnly(Array.Empty<WorkflowDynamicPortRule>());
    private static readonly IReadOnlyList<JsonObject> _emptyExamples = Array.AsReadOnly(Array.Empty<JsonObject>());
    private readonly JsonObject? _parametersSchema;
    private readonly IReadOnlyList<JsonObject> _parameterExamples;

    /// <summary>
    /// Initializes a catalog node definition.
    /// </summary>
    /// <param name="type">The namespace-style node type identifier.</param>
    /// <param name="version">The node type version.</param>
    /// <param name="displayName">Optional display name for authoring surfaces.</param>
    /// <param name="description">Optional human-readable node description.</param>
    /// <param name="category">Optional catalog category.</param>
    /// <param name="parametersSchema">Optional JSON schema fragment describing node parameters.</param>
    /// <param name="inputs">Input ports keyed by port name.</param>
    /// <param name="outputs">Output ports keyed by port name.</param>
    /// <param name="dynamicPorts">Dynamic port derivation rules.</param>
    /// <param name="resources">Resource slots keyed by slot name.</param>
    /// <param name="capabilities">Ordered node capability identifiers.</param>
    /// <param name="behavior">High-level non-executable behavior metadata.</param>
    /// <param name="stability">Stability metadata for this definition.</param>
    /// <param name="deprecation">Optional deprecation metadata.</param>
    /// <param name="parameterExamples">Immutable example parameter objects.</param>
    /// <param name="locators">Locator slots keyed by slot name.</param>
    public WorkflowNodeDefinition(
        string type,
        int version,
        string? displayName = null,
        string? description = null,
        string? category = null,
        JsonObject? parametersSchema = null,
        IReadOnlyDictionary<string, WorkflowPortDefinition>? inputs = null,
        IReadOnlyDictionary<string, WorkflowPortDefinition>? outputs = null,
        IReadOnlyList<WorkflowDynamicPortRule>? dynamicPorts = null,
        IReadOnlyDictionary<string, WorkflowNodeResourceRequirement>? resources = null,
        IReadOnlyList<string>? capabilities = null,
        WorkflowNodeBehaviorMetadata? behavior = null,
        WorkflowNodeStability stability = WorkflowNodeStability.Preview,
        WorkflowNodeDeprecationMetadata? deprecation = null,
        IReadOnlyList<JsonObject>? parameterExamples = null,
        IReadOnlyDictionary<string, NodeLocatorSlotDefinition>? locators = null)
    {
        Type = type;
        Version = version;
        DisplayName = displayName;
        Description = description;
        Category = category;
        _parametersSchema = parametersSchema is null ? null : (JsonObject)parametersSchema.DeepClone();
        Inputs = inputs is null ? _emptyPorts : new ReadOnlyDictionary<string, WorkflowPortDefinition>(new Dictionary<string, WorkflowPortDefinition>(inputs));
        Outputs = outputs is null ? _emptyPorts : new ReadOnlyDictionary<string, WorkflowPortDefinition>(new Dictionary<string, WorkflowPortDefinition>(outputs));
        DynamicPorts = dynamicPorts is null ? _emptyDynamicPortRules : new ReadOnlyCollection<WorkflowDynamicPortRule>([.. dynamicPorts]);
        Resources = resources is null ? _emptyResources : new ReadOnlyDictionary<string, WorkflowNodeResourceRequirement>(new Dictionary<string, WorkflowNodeResourceRequirement>(resources));
        Capabilities = capabilities is null ? _emptyCapabilities : Array.AsReadOnly([.. capabilities]);
        Locators = locators is null ? _emptyLocators : new ReadOnlyDictionary<string, NodeLocatorSlotDefinition>(new Dictionary<string, NodeLocatorSlotDefinition>(locators));
        Behavior = behavior ?? new WorkflowNodeBehaviorMetadata();
        Stability = stability;
        Deprecation = deprecation ?? new WorkflowNodeDeprecationMetadata();
        _parameterExamples = parameterExamples is null ? _emptyExamples : CloneExamples(parameterExamples);
    }

    /// <summary>
    /// Gets the namespace-style node type identifier.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the node type version.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the versioned catalog key for this definition.
    /// </summary>
    public WorkflowNodeDefinitionKey Key => new(Type, Version);

    /// <summary>
    /// Gets an optional display name for authoring surfaces.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Gets an optional human-readable node description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets an optional catalog category.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Gets a defensive clone of the optional JSON schema fragment describing node parameters.
    /// </summary>
    public JsonObject? ParametersSchema => _parametersSchema is null ? null : (JsonObject)_parametersSchema.DeepClone();

    /// <summary>
    /// Gets input ports keyed by port name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowPortDefinition> Inputs { get; }

    /// <summary>
    /// Gets output ports keyed by port name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowPortDefinition> Outputs { get; }

    /// <summary>
    /// Gets dynamic port derivation rules.
    /// </summary>
    public IReadOnlyList<WorkflowDynamicPortRule> DynamicPorts { get; }

    /// <summary>
    /// Gets resource slots keyed by slot name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowNodeResourceRequirement> Resources { get; }

    /// <summary>
    /// Gets locator slots keyed by slot name.
    /// </summary>
    public IReadOnlyDictionary<string, NodeLocatorSlotDefinition> Locators { get; }

    /// <summary>
    /// Gets ordered node capability identifiers.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Gets high-level non-executable behavior metadata.
    /// </summary>
    public WorkflowNodeBehaviorMetadata Behavior { get; }

    /// <summary>
    /// Gets stability metadata for this definition.
    /// </summary>
    public WorkflowNodeStability Stability { get; }

    /// <summary>
    /// Gets optional deprecation metadata.
    /// </summary>
    public WorkflowNodeDeprecationMetadata Deprecation { get; }

    /// <summary>
    /// Gets defensive clones of example parameter objects.
    /// </summary>
    public IReadOnlyList<JsonObject> ParameterExamples => CloneExamples(_parameterExamples);

    private static IReadOnlyList<JsonObject> CloneExamples(IReadOnlyList<JsonObject> examples)
    {
        var clone = new JsonObject[examples.Count];

        for (int index = 0; index < examples.Count; index++)
        {
            clone[index] = (JsonObject)examples[index].DeepClone();
        }

        return Array.AsReadOnly(clone);
    }
}
