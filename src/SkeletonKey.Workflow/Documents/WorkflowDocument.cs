using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;
using SkeletonKey.Workflow.Inputs;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Outputs;
using SkeletonKey.Workflow.Resources;
using SkeletonKey.Workflow.Specification;

namespace SkeletonKey.Workflow.Documents;

/// <summary>
/// Represents the immutable root document for a graph-based workflow.
/// </summary>
public sealed class WorkflowDocument
{
    private static readonly IReadOnlyDictionary<string, WorkflowInputDefinition> _emptyInputs = new ReadOnlyDictionary<string, WorkflowInputDefinition>(new Dictionary<string, WorkflowInputDefinition>());
    private static readonly IReadOnlyDictionary<string, JsonNode?> _emptyVariables = new ReadOnlyDictionary<string, JsonNode?>(new Dictionary<string, JsonNode?>());
    private static readonly IReadOnlyDictionary<string, WorkflowResourceDefinition> _emptyResources = new ReadOnlyDictionary<string, WorkflowResourceDefinition>(new Dictionary<string, WorkflowResourceDefinition>());
    private static readonly IReadOnlyList<WorkflowNode> _emptyNodes = Array.AsReadOnly(Array.Empty<WorkflowNode>());
    private static readonly IReadOnlyList<WorkflowConnection> _emptyConnections = Array.AsReadOnly(Array.Empty<WorkflowConnection>());
    private static readonly IReadOnlyDictionary<string, WorkflowOutputDefinition> _emptyOutputs = new ReadOnlyDictionary<string, WorkflowOutputDefinition>(new Dictionary<string, WorkflowOutputDefinition>());

    private readonly IReadOnlyDictionary<string, JsonNode?> _variables;

    /// <summary>
    /// Initializes a new workflow document.
    /// </summary>
    /// <param name="schema">The workflow schema URI declaration.</param>
    /// <param name="specVersion">The workflow language specification version declaration.</param>
    /// <param name="id">The workflow identifier.</param>
    /// <param name="name">The workflow display name.</param>
    /// <param name="description">Optional human-readable workflow description.</param>
    /// <param name="inputs">Optional declared workflow inputs.</param>
    /// <param name="variables">Optional initial workflow variables.</param>
    /// <param name="resources">Optional declared resource requirements.</param>
    /// <param name="nodes">Optional graph node declarations.</param>
    /// <param name="connections">Optional graph connection declarations.</param>
    /// <param name="outputs">Optional workflow output declarations.</param>
    /// <param name="designer">Optional designer-only metadata.</param>
    public WorkflowDocument(
        string schema = WorkflowSpecification.CurrentSchemaUri,
        string specVersion = WorkflowSpecification.CurrentVersion,
        string id = "",
        string name = "",
        string? description = null,
        IReadOnlyDictionary<string, WorkflowInputDefinition>? inputs = null,
        IReadOnlyDictionary<string, JsonNode?>? variables = null,
        IReadOnlyDictionary<string, WorkflowResourceDefinition>? resources = null,
        IReadOnlyList<WorkflowNode>? nodes = null,
        IReadOnlyList<WorkflowConnection>? connections = null,
        IReadOnlyDictionary<string, WorkflowOutputDefinition>? outputs = null,
        WorkflowDesignerMetadata? designer = null)
    {
        Schema = schema;
        SpecVersion = specVersion;
        Id = id;
        Name = name;
        Description = description;
        Inputs = inputs is null
            ? _emptyInputs
            : new ReadOnlyDictionary<string, WorkflowInputDefinition>(new Dictionary<string, WorkflowInputDefinition>(inputs));
        _variables = variables is null ? _emptyVariables : CloneVariables(variables);
        Resources = resources is null
            ? _emptyResources
            : new ReadOnlyDictionary<string, WorkflowResourceDefinition>(new Dictionary<string, WorkflowResourceDefinition>(resources));
        Nodes = nodes is null ? _emptyNodes : Array.AsReadOnly([.. nodes]);
        Connections = connections is null ? _emptyConnections : Array.AsReadOnly([.. connections]);
        Outputs = outputs is null
            ? _emptyOutputs
            : new ReadOnlyDictionary<string, WorkflowOutputDefinition>(new Dictionary<string, WorkflowOutputDefinition>(outputs));
        Designer = designer;
    }

    /// <summary>
    /// Gets the workflow schema URI declaration.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Gets the workflow language specification version declaration.
    /// </summary>
    public string SpecVersion { get; }

    /// <summary>
    /// Gets the workflow identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the workflow display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets an optional human-readable workflow description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets declared workflow inputs keyed by input name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowInputDefinition> Inputs { get; }

    /// <summary>
    /// Gets defensive copies of initial workflow variables keyed by variable name.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> Variables => CloneVariables(_variables);

    /// <summary>
    /// Gets declared provider-neutral workflow resource requirements keyed by resource name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowResourceDefinition> Resources { get; }

    /// <summary>
    /// Gets graph node declarations.
    /// </summary>
    public IReadOnlyList<WorkflowNode> Nodes { get; }

    /// <summary>
    /// Gets directed graph connection declarations.
    /// </summary>
    public IReadOnlyList<WorkflowConnection> Connections { get; }

    /// <summary>
    /// Gets workflow output declarations keyed by output name.
    /// </summary>
    public IReadOnlyDictionary<string, WorkflowOutputDefinition> Outputs { get; }

    /// <summary>
    /// Gets optional designer-only metadata.
    /// </summary>
    public WorkflowDesignerMetadata? Designer { get; }

    private static IReadOnlyDictionary<string, JsonNode?> CloneVariables(IReadOnlyDictionary<string, JsonNode?> variables)
    {
        Dictionary<string, JsonNode?> clone = new(variables.Count);

        foreach (KeyValuePair<string, JsonNode?> variable in variables)
        {
            clone[variable.Key] = variable.Value?.DeepClone();
        }

        return new ReadOnlyDictionary<string, JsonNode?>(clone);
    }
}

