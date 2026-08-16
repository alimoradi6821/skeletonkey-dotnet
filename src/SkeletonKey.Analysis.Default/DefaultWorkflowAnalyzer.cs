using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SkeletonKey.Catalog;
using SkeletonKey.Locators;
using SkeletonKey.Locators.Runtime;
using SkeletonKey.Resources;
using SkeletonKey.Validation;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Documents;
using SkeletonKey.Workflow.Nodes;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Analysis.Default;

/// <summary>
/// Provides the default deterministic catalog-aware workflow analyzer.
/// </summary>
/// <remarks>
/// The analyzer is stateless after construction, thread-safe, and pure with respect to supplied
/// workflows and catalogs. It resolves exact catalog identity, effective ports, parameter contracts,
/// resources, capabilities, and connection compatibility without executing workflows or nodes.
/// </remarks>
public sealed partial class DefaultWorkflowAnalyzer : IWorkflowAnalyzer
{
    private static readonly IReadOnlyList<string> _controlRole = Array.AsReadOnly(["control"]);
    private readonly IWorkflowNodeDefinitionCatalog? _catalog;
    private readonly WorkflowAnalysisOptions _options;
    private readonly WorkflowSemanticValidator _semanticValidator = new();
    private readonly WorkflowResourceReferenceReader _resourceReader = new();
    private readonly LocatorReferenceReader _locatorReader = new();
    private readonly ILocatorPlanResolver? _locatorResolver;

    /// <summary>
    /// Initializes an analyzer that receives its catalog from <see cref="IWorkflowAnalyzer.Analyze" />.
    /// </summary>
    /// <param name="options">Optional immutable deterministic analysis options.</param>
    /// <param name="locatorResolver">Optional explicit locator resolver used for repository-backed locator analysis.</param>
    public DefaultWorkflowAnalyzer(WorkflowAnalysisOptions? options = null, ILocatorPlanResolver? locatorResolver = null)
    {
        _options = options ?? WorkflowAnalysisOptions.Default;
        _locatorResolver = locatorResolver;
    }

    /// <summary>
    /// Initializes an analyzer with a default catalog for direct construction without a service container.
    /// </summary>
    /// <param name="catalog">The deterministic node definition catalog.</param>
    /// <param name="options">Optional immutable deterministic analysis options.</param>
    /// <param name="locatorResolver">Optional explicit locator resolver used for repository-backed locator analysis.</param>
    public DefaultWorkflowAnalyzer(IWorkflowNodeDefinitionCatalog catalog, WorkflowAnalysisOptions? options = null, ILocatorPlanResolver? locatorResolver = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _options = options ?? WorkflowAnalysisOptions.Default;
        _locatorResolver = locatorResolver;
    }

    /// <inheritdoc />
    public WorkflowAnalysisResult Analyze(WorkflowDocument workflow, IWorkflowNodeDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(catalog);
        return AnalyzeCore(workflow, catalog);
    }

    /// <summary>
    /// Analyzes a workflow using the catalog supplied to the constructor.
    /// </summary>
    /// <param name="workflow">The workflow document to analyze.</param>
    /// <returns>The immutable catalog-aware analysis result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no constructor catalog was supplied.</exception>
    public WorkflowAnalysisResult Analyze(WorkflowDocument workflow)
    {
        if (_catalog is null)
        {
            throw new InvalidOperationException("A catalog must be supplied before analysis.");
        }

        return AnalyzeCore(workflow, _catalog);
    }

    private WorkflowAnalysisResult AnalyzeCore(WorkflowDocument workflow, IWorkflowNodeDefinitionCatalog catalog)
    {
        IssueSink issues = new(_options.MaximumIssues);
        foreach (WorkflowValidationIssue validationIssue in _semanticValidator.Validate(workflow).Errors)
        {
            issues.Add(new(
                WorkflowAnalysisCodes.SemanticValidationError,
                WorkflowAnalysisSeverity.Error,
                validationIssue.Message,
                validationIssue.Path));
        }

        Dictionary<string, WorkflowNode> nodesById = new(StringComparer.Ordinal);
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            WorkflowNode node = workflow.Nodes[index];
            nodesById.TryAdd(node.Id, node);
        }

        List<NodeState> nodeStates = [];
        for (int index = 0; index < workflow.Nodes.Count; index++)
        {
            nodeStates.Add(AnalyzeNode(workflow, catalog, workflow.Nodes[index], index, issues));
        }

        List<WorkflowConnectionAnalysis> connections = AnalyzeConnections(workflow, nodeStates, issues);
        AnalyzeMultiplicity(workflow, connections, issues);

        IReadOnlyList<WorkflowAnalysisIssue> orderedIssues = OrderIssues(issues.Issues);
        return new WorkflowAnalysisResult(
            workflow.Id,
            CatalogIdentity(catalog),
            null,
            nodeStates.Select(static state => state.Analysis).ToArray(),
            connections,
            orderedIssues,
            workflow.SpecVersion);
    }

    private NodeState AnalyzeNode(
        WorkflowDocument workflow,
        IWorkflowNodeDefinitionCatalog catalog,
        WorkflowNode node,
        int nodeIndex,
        IssueSink issues)
    {
        string nodePath = Pointer("nodes", nodeIndex);
        WorkflowNodeDefinition? definition = ResolveDefinition(catalog, node, nodePath, issues, out WorkflowNodeCatalogStatus catalogStatus);
        List<WorkflowEffectivePort> ports = [];
        List<WorkflowResourceSlotAnalysis> resources = [];
        List<WorkflowLocatorSlotAnalysis> locators = [];
        List<WorkflowAnalysisIssue> nodeIssues = [];
        WorkflowParameterAnalysisStatus parameterStatus = WorkflowParameterAnalysisStatus.NotAnalyzed;
        WorkflowResourceRequirementAnalysisStatus resourceStatus = WorkflowResourceRequirementAnalysisStatus.NotAnalyzed;
        WorkflowCapabilityCompatibilityStatus capabilityStatus = WorkflowCapabilityCompatibilityStatus.NotAnalyzed;

        if (definition is not null)
        {
            ports.AddRange(ResolveStaticPorts(definition));
            ResolveDynamicPorts(node, nodeIndex, definition, ports, issues);
            AnalyzeDuplicateEffectivePorts(node, nodeIndex, ports, issues);
            parameterStatus = AnalyzeParameters(node, nodeIndex, definition, issues);
            resources.AddRange(AnalyzeResources(workflow, node, nodeIndex, definition, issues));
            locators.AddRange(AnalyzeLocators(node, nodeIndex, definition, issues));
            resourceStatus = AggregateResourceStatus(resources);
            capabilityStatus = resourceStatus is WorkflowResourceRequirementAnalysisStatus.MissingRequiredCapability
                ? WorkflowCapabilityCompatibilityStatus.MissingRequiredCapability
                : WorkflowCapabilityCompatibilityStatus.Compatible;
            AnalyzeDeprecated(definition, node, nodePath, issues);
        }

        WorkflowNodeAnalysis analysis = new(
            node.Id,
            node.Type,
            node.TypeVersion,
            node.Disabled,
            catalogStatus,
            definition,
            parameterStatus,
            resourceStatus,
            capabilityStatus,
            nodeIssues,
            ports,
            resources,
            locators);

        Dictionary<string, WorkflowEffectivePort> portsById = new(StringComparer.Ordinal);
        foreach (WorkflowEffectivePort port in ports)
        {
            portsById.TryAdd(port.Id, port);
        }

        return new NodeState(node, nodeIndex, analysis, portsById);
    }

    private static WorkflowNodeDefinition? ResolveDefinition(
        IWorkflowNodeDefinitionCatalog catalog,
        WorkflowNode node,
        string nodePath,
        IssueSink issues,
        out WorkflowNodeCatalogStatus status)
    {
        int exactCount = catalog.Definitions.Count(definition =>
            string.Equals(definition.Type, node.Type, StringComparison.Ordinal) &&
            definition.Version == node.TypeVersion);
        if (exactCount > 1)
        {
            status = WorkflowNodeCatalogStatus.UnknownVersion;
            issues.Add(new(
                WorkflowAnalysisCodes.CatalogDefinitionConflict,
                WorkflowAnalysisSeverity.Error,
                $"Catalog contains more than one definition for '{node.Type}' version {node.TypeVersion}.",
                nodePath,
                node.Id,
                node.Type));
            return null;
        }

        if (catalog.TryGetDefinition(node.Type, node.TypeVersion, out WorkflowNodeDefinition? definition))
        {
            status = WorkflowNodeCatalogStatus.Known;
            return definition;
        }

        if (catalog.GetDefinitions(node.Type).Count == 0)
        {
            status = WorkflowNodeCatalogStatus.UnknownType;
            issues.Add(new(
                WorkflowAnalysisCodes.UnknownNodeType,
                WorkflowAnalysisSeverity.Error,
                $"Node type '{node.Type}' is not present in the supplied catalog.",
                Pointer(nodePath, "type"),
                node.Id,
                node.Type));
            return null;
        }

        status = WorkflowNodeCatalogStatus.UnknownVersion;
        issues.Add(new(
            WorkflowAnalysisCodes.UnknownNodeVersion,
            WorkflowAnalysisSeverity.Error,
            $"Node type '{node.Type}' does not declare exact version {node.TypeVersion}.",
            Pointer(nodePath, "typeVersion"),
            node.Id,
            node.Type));
        return null;
    }

    private static IEnumerable<WorkflowEffectivePort> ResolveStaticPorts(WorkflowNodeDefinition definition)
    {
        foreach (WorkflowPortDefinition port in definition.Inputs.Values.OrderBy(static port => port.Name, StringComparer.Ordinal))
        {
            yield return CreateEffectivePort(port, WorkflowEffectivePortOrigin.Static, "/inputs/" + Escape(port.Name), null);
        }

        foreach (WorkflowPortDefinition port in definition.Outputs.Values.OrderBy(static port => port.Name, StringComparer.Ordinal))
        {
            yield return CreateEffectivePort(port, WorkflowEffectivePortOrigin.Static, "/outputs/" + Escape(port.Name), null);
        }
    }

    private static WorkflowEffectivePort CreateEffectivePort(
        WorkflowPortDefinition port,
        WorkflowEffectivePortOrigin origin,
        string? originPath,
        WorkflowDynamicPortRuleKind? sourceRuleKind)
    {
        return new(
            port.Name,
            port.Direction,
            port.Required,
            port.AllowsMultiple,
            port.Roles,
            origin,
            originPath,
            sourceRuleKind);
    }

    private static void ResolveDynamicPorts(
        WorkflowNode node,
        int nodeIndex,
        WorkflowNodeDefinition definition,
        List<WorkflowEffectivePort> ports,
        IssueSink issues)
    {
        JsonObject parameters = node.Parameters;
        HashSet<string> known = new(ports.Select(static port => port.Id), StringComparer.Ordinal);

        foreach (WorkflowDynamicPortRule rule in definition.DynamicPorts)
        {
            string sourcePath = Pointer("nodes", nodeIndex, "parameters") + rule.SourcePointer;
            if (!TryResolvePointer(parameters, rule.SourcePointer, out JsonNode? source))
            {
                AddDynamicIssue(issues, node, sourcePath, "Dynamic port source parameter data is not statically available.");
                continue;
            }

            if (ContainsReservedNonLiteral(source))
            {
                AddDynamicIssue(issues, node, sourcePath, "Dynamic port source data must be literal parameter data.");
                continue;
            }

            if (source is not JsonArray array)
            {
                AddDynamicIssue(issues, node, sourcePath, "Dynamic port source parameter must resolve to an array.");
                continue;
            }

            for (int index = 0; index < array.Count; index++)
            {
                JsonNode? item = array[index];
                string itemPath = Pointer(sourcePath, index);
                if (item is not JsonObject)
                {
                    AddDynamicIssue(issues, node, itemPath, "Dynamic port source item must be an object.");
                    continue;
                }

                if (!TryResolvePointer(item, rule.IdPointer, out JsonNode? idNode) ||
                    idNode is null ||
                    idNode.GetValueKind() is not JsonValueKind.String)
                {
                    AddDynamicIssue(issues, node, itemPath + rule.IdPointer, "Dynamic port ID must resolve to a string.");
                    continue;
                }

                string id = idNode.GetValue<string>();
                if (!PortIdRegex().IsMatch(id))
                {
                    AddDynamicIssue(issues, node, itemPath + rule.IdPointer, $"Dynamic port ID '{id}' is invalid.");
                    continue;
                }

                if (!known.Add(id))
                {
                    AddDynamicIssue(issues, node, itemPath + rule.IdPointer, $"Dynamic port ID '{id}' conflicts with another effective port.");
                    continue;
                }

                ports.Add(new WorkflowEffectivePort(
                    id,
                    rule.Direction,
                    roles: _controlRole,
                    origin: WorkflowEffectivePortOrigin.Dynamic,
                    originPath: itemPath + rule.IdPointer,
                    sourceRuleKind: rule.Kind));
            }
        }
    }

    private WorkflowParameterAnalysisStatus AnalyzeParameters(
        WorkflowNode node,
        int nodeIndex,
        WorkflowNodeDefinition definition,
        IssueSink issues)
    {
        if (!_options.ValidateParameterSchemas)
        {
            return WorkflowParameterAnalysisStatus.NotAnalyzed;
        }

        JsonObject? schema = definition.ParametersSchema;
        if (schema is null)
        {
            return WorkflowParameterAnalysisStatus.UnknownSchema;
        }

        JsonObject parameters = node.Parameters;
        if (!schema.TryGetPropertyValue("required", out JsonNode? requiredNode) || requiredNode is not JsonArray required)
        {
            return WorkflowParameterAnalysisStatus.UnknownSchema;
        }

        bool valid = true;
        for (int index = 0; index < required.Count; index++)
        {
            JsonNode? item = required[index];
            if (item is null || item.GetValueKind() is not JsonValueKind.String)
            {
                continue;
            }

            string name = item.GetValue<string>();
            if (!parameters.ContainsKey(name))
            {
                valid = false;
                issues.Add(new(
                    WorkflowAnalysisCodes.InvalidNodeParameters,
                    WorkflowAnalysisSeverity.Error,
                    $"Required parameter '{name}' is missing.",
                    Pointer("nodes", nodeIndex, "parameters", name),
                    node.Id,
                    node.Type));
            }
        }

        return valid ? WorkflowParameterAnalysisStatus.Valid : WorkflowParameterAnalysisStatus.Invalid;
    }

    private IEnumerable<WorkflowResourceSlotAnalysis> AnalyzeResources(
        WorkflowDocument workflow,
        WorkflowNode node,
        int nodeIndex,
        WorkflowNodeDefinition definition,
        IssueSink issues)
    {
        JsonObject parameters = node.Parameters;
        foreach (WorkflowNodeResourceRequirement requirement in definition.Resources.Values.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            string parameterPath = Pointer("nodes", nodeIndex, "parameters", requirement.Name);
            if (!parameters.TryGetPropertyValue(requirement.Name, out JsonNode? wrapper))
            {
                if (requirement.Required)
                {
                    issues.Add(new(
                        WorkflowAnalysisCodes.MissingRequiredResource,
                        WorkflowAnalysisSeverity.Error,
                        $"Required resource slot '{requirement.Name}' is not mapped.",
                        parameterPath,
                        node.Id,
                        node.Type));
                    yield return new(requirement.Name, null, true, requirement.Kind, null, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.MissingRequiredResource, parameterPath);
                }
                else
                {
                    yield return new(requirement.Name, null, false, requirement.Kind, null, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.Satisfied, parameterPath);
                }

                continue;
            }

            WorkflowResourceReference? reference = null;
            WorkflowResourceReferenceFormatException? formatException = null;
            try
            {
                reference = wrapper is null ? null : _resourceReader.Read(wrapper);
            }
            catch (WorkflowResourceReferenceFormatException exception)
            {
                formatException = exception;
            }

            if (formatException is not null || reference is null)
            {
                string message = formatException?.Message ?? "Resource reference wrapper is required.";
                string path = formatException is null ? parameterPath : Pointer(parameterPath, formatException.JsonPath);
                issues.Add(new(WorkflowAnalysisCodes.InvalidResourceReference, WorkflowAnalysisSeverity.Error, message, path, node.Id, node.Type));
                yield return new(requirement.Name, null, requirement.Required, requirement.Kind, null, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.MissingRequiredResource, parameterPath);
                continue;
            }

            if (!workflow.Resources.TryGetValue(reference.Name, out WorkflowResourceDefinition? resource))
            {
                if (requirement.Required)
                {
                    issues.Add(new(
                        WorkflowAnalysisCodes.MissingRequiredResource,
                        WorkflowAnalysisSeverity.Error,
                        $"Workflow resource '{reference.Name}' is not declared.",
                        parameterPath,
                        node.Id,
                        node.Type));
                    yield return new(requirement.Name, reference.Name, true, requirement.Kind, null, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.MissingRequiredResource, parameterPath);
                }
                else
                {
                    yield return new(requirement.Name, reference.Name, false, requirement.Kind, null, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.Satisfied, parameterPath);
                }

                continue;
            }

            if (!string.Equals(resource.Kind, requirement.Kind, StringComparison.Ordinal))
            {
                issues.Add(new(
                    WorkflowAnalysisCodes.ResourceKindMismatch,
                    WorkflowAnalysisSeverity.Error,
                    $"Workflow resource '{reference.Name}' kind '{resource.Kind}' does not match required kind '{requirement.Kind}'.",
                    parameterPath,
                    node.Id,
                    node.Type));
                yield return new(requirement.Name, reference.Name, requirement.Required, requirement.Kind, resource.Access, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.IncompatibleResourceKind, parameterPath);
                continue;
            }

            string? missing = _options.RequireResourceCapabilities
                ? requirement.Capabilities.FirstOrDefault(capability => !resource.Capabilities.Contains(capability, StringComparer.Ordinal))
                : null;
            if (missing is not null)
            {
                issues.Add(new(
                    WorkflowAnalysisCodes.MissingResourceCapability,
                    WorkflowAnalysisSeverity.Error,
                    $"Workflow resource '{reference.Name}' is missing required capability '{missing}'.",
                    parameterPath,
                    node.Id,
                    node.Type));
                yield return new(requirement.Name, reference.Name, requirement.Required, requirement.Kind, resource.Access, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.MissingRequiredCapability, parameterPath);
                continue;
            }

            yield return new(requirement.Name, reference.Name, requirement.Required, requirement.Kind, resource.Access, requirement.Capabilities, WorkflowResourceRequirementAnalysisStatus.Satisfied, parameterPath);
        }
    }

    private IEnumerable<WorkflowLocatorSlotAnalysis> AnalyzeLocators(
        WorkflowNode node,
        int nodeIndex,
        WorkflowNodeDefinition definition,
        IssueSink issues)
    {
        JsonObject parameters = node.Parameters;
        foreach (NodeLocatorSlotDefinition slot in definition.Locators.Values.OrderBy(static value => value.Name, StringComparer.Ordinal))
        {
            string parameterPath = Pointer("nodes", nodeIndex, "parameters") + slot.ParameterPointer;
            if (!TryResolvePointer(parameters, slot.ParameterPointer, out JsonNode? wrapper))
            {
                if (slot.Required)
                {
                    issues.Add(new(
                        WorkflowAnalysisCodes.MissingRequiredLocator,
                        WorkflowAnalysisSeverity.Error,
                        $"Required locator slot '{slot.Name}' is not mapped.",
                        parameterPath,
                        node.Id,
                        node.Type));
                    yield return new(slot.Name, null, null, true, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.MissingRequiredLocator, parameterPath);
                }
                else
                {
                    yield return new(slot.Name, null, null, false, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.Satisfied, parameterPath);
                }

                continue;
            }

            LocatorReference? reference = null;
            LocatorReferenceFormatException? formatException = null;
            try
            {
                reference = wrapper is null ? null : _locatorReader.Read(wrapper);
            }
            catch (LocatorReferenceFormatException exception)
            {
                formatException = exception;
            }

            if (formatException is not null)
            {
                issues.Add(new(WorkflowAnalysisCodes.InvalidLocatorReference, WorkflowAnalysisSeverity.Error, formatException.Message, Pointer(parameterPath, formatException.JsonPath), node.Id, node.Type));
                yield return new(slot.Name, null, null, slot.Required, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.InvalidLocatorReference, parameterPath);
                continue;
            }

            if (reference is null)
            {
                issues.Add(new(WorkflowAnalysisCodes.InvalidLocatorReference, WorkflowAnalysisSeverity.Error, "Locator reference wrapper is required.", parameterPath, node.Id, node.Type));
                yield return new(slot.Name, null, null, slot.Required, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.InvalidLocatorReference, parameterPath);
                continue;
            }

            ResolvedLocatorPlan? plan = null;
            if (_locatorResolver is not null)
            {
                LocatorPlanResolutionException? resolutionException = null;
                try
                {
                    plan = _locatorResolver.ResolveAsync(reference).AsTask().GetAwaiter().GetResult();
                }
                catch (LocatorPlanResolutionException exception)
                {
                    resolutionException = exception;
                }

                if (resolutionException is not null)
                {
                    WorkflowLocatorSlotAnalysisStatus status = resolutionException.Code == LocatorPlanResolutionCodes.DocumentNotFound
                        ? WorkflowLocatorSlotAnalysisStatus.UnknownLocatorDocument
                        : WorkflowLocatorSlotAnalysisStatus.UnknownLocatorId;
                    issues.Add(new(WorkflowAnalysisCodes.LocatorResolutionFailed, WorkflowAnalysisSeverity.Error, resolutionException.Message, parameterPath, node.Id, node.Type));
                    yield return new(slot.Name, reference, null, slot.Required, slot.Usage, slot.AcceptedCardinalities, status, parameterPath);
                    continue;
                }

                if (plan is not null && !slot.AcceptedCardinalities.Contains(plan.Cardinality))
                {
                    issues.Add(new(WorkflowAnalysisCodes.LocatorCardinalityMismatch, WorkflowAnalysisSeverity.Error, $"Locator slot '{slot.Name}' does not accept cardinality '{plan.Cardinality}'.", parameterPath, node.Id, node.Type));
                    yield return new(slot.Name, reference, plan, slot.Required, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.CardinalityMismatch, parameterPath);
                    continue;
                }
            }

            yield return new(slot.Name, reference, plan, slot.Required, slot.Usage, slot.AcceptedCardinalities, WorkflowLocatorSlotAnalysisStatus.Satisfied, parameterPath);
        }
    }

    private void AnalyzeDeprecated(WorkflowNodeDefinition definition, WorkflowNode node, string nodePath, IssueSink issues)
    {
        if (!definition.Deprecation.Deprecated)
        {
            return;
        }

        WorkflowAnalysisSeverity severity = _options.TreatDeprecatedNodesAsErrors ? WorkflowAnalysisSeverity.Error : WorkflowAnalysisSeverity.Warning;
        string message = definition.Deprecation.Message ?? $"Node definition '{definition.Type}' version {definition.Version} is deprecated.";
        if (definition.Deprecation.ReplacementType is not null)
        {
            message += $" Replacement: {definition.Deprecation.ReplacementType}";
            if (definition.Deprecation.ReplacementVersion is not null)
            {
                message += $" version {definition.Deprecation.ReplacementVersion.Value}";
            }
        }

        issues.Add(new(WorkflowAnalysisCodes.DeprecatedNodeDefinition, severity, message, nodePath, node.Id, node.Type));
    }

    private static List<WorkflowConnectionAnalysis> AnalyzeConnections(
        WorkflowDocument workflow,
        IReadOnlyList<NodeState> states,
        IssueSink issues)
    {
        var byNode = states.ToDictionary(static state => state.Node.Id, static state => state, StringComparer.Ordinal);
        List<WorkflowConnectionAnalysis> result = [];

        for (int index = 0; index < workflow.Connections.Count; index++)
        {
            WorkflowConnection connection = workflow.Connections[index];
            string path = Pointer("connections", index);
            List<WorkflowAnalysisIssue> connectionIssues = [];
            WorkflowEffectivePort? sourcePort = ResolvePort(byNode, connection.From, WorkflowPortDirection.Output, true, index, issues, connectionIssues, out WorkflowPortCatalogStatus sourceStatus);
            WorkflowEffectivePort? targetPort = ResolvePort(byNode, connection.To, WorkflowPortDirection.Input, false, index, issues, connectionIssues, out WorkflowPortCatalogStatus targetStatus);
            WorkflowConnectionRoleCompatibilityStatus roleStatus = RoleStatus(sourcePort, targetPort, sourceStatus, targetStatus, path, issues, connectionIssues);
            WorkflowDynamicPortAnalysisStatus dynamicStatus = DynamicStatus(sourcePort, targetPort, sourceStatus, targetStatus);

            result.Add(new(
                connection.From.Node,
                connection.From.Port,
                connection.To.Node,
                connection.To.Port,
                sourceStatus,
                targetStatus,
                index,
                dynamicStatus,
                roleStatus,
                connectionIssues,
                sourcePort,
                targetPort));
        }

        return result;
    }

    private static WorkflowEffectivePort? ResolvePort(
        IReadOnlyDictionary<string, NodeState> states,
        WorkflowEndpoint endpoint,
        WorkflowPortDirection expectedDirection,
        bool source,
        int connectionIndex,
        IssueSink issues,
        List<WorkflowAnalysisIssue> connectionIssues,
        out WorkflowPortCatalogStatus status)
    {
        string role = source ? "source" : "target";
        string issueCode = source ? WorkflowAnalysisCodes.UnknownSourcePort : WorkflowAnalysisCodes.UnknownTargetPort;
        string path = Pointer("connections", connectionIndex, source ? "from" : "to", "port");

        if (!states.TryGetValue(endpoint.Node, out NodeState? state) || state.Analysis.CatalogStatus != WorkflowNodeCatalogStatus.Known)
        {
            status = WorkflowPortCatalogStatus.UnknownNode;
            return null;
        }

        if (!state.Ports.TryGetValue(endpoint.Port, out WorkflowEffectivePort? port))
        {
            status = WorkflowPortCatalogStatus.UnknownPort;
            WorkflowAnalysisIssue issue = new(
                issueCode,
                WorkflowAnalysisSeverity.Error,
                $"Connection {role} port '{endpoint.Port}' is not declared by node '{endpoint.Node}'.",
                path,
                endpoint.Node,
                state.Node.Type);
            issues.Add(issue);
            connectionIssues.Add(issue);
            return null;
        }

        if (port.Direction != expectedDirection)
        {
            status = WorkflowPortCatalogStatus.WrongDirection;
            WorkflowAnalysisIssue issue = new(
                WorkflowAnalysisCodes.InvalidPortDirection,
                WorkflowAnalysisSeverity.Error,
                $"Connection {role} port '{endpoint.Port}' has direction '{port.Direction}' but expected '{expectedDirection}'.",
                path,
                endpoint.Node,
                state.Node.Type);
            issues.Add(issue);
            connectionIssues.Add(issue);
            return port;
        }

        status = WorkflowPortCatalogStatus.Known;
        return port;
    }

    private static WorkflowConnectionRoleCompatibilityStatus RoleStatus(
        WorkflowEffectivePort? source,
        WorkflowEffectivePort? target,
        WorkflowPortCatalogStatus sourceStatus,
        WorkflowPortCatalogStatus targetStatus,
        string path,
        IssueSink issues,
        List<WorkflowAnalysisIssue> connectionIssues)
    {
        if (sourceStatus == WorkflowPortCatalogStatus.WrongDirection || targetStatus == WorkflowPortCatalogStatus.WrongDirection)
        {
            return WorkflowConnectionRoleCompatibilityStatus.InvalidDirection;
        }

        if (source is null || target is null)
        {
            return WorkflowConnectionRoleCompatibilityStatus.NotAnalyzed;
        }

        bool compatible = source.Roles.Any(role => target.Roles.Contains(role, StringComparer.Ordinal));
        if (compatible)
        {
            return WorkflowConnectionRoleCompatibilityStatus.Compatible;
        }

        WorkflowAnalysisIssue issue = new(
            WorkflowAnalysisCodes.IncompatiblePortRoles,
            WorkflowAnalysisSeverity.Error,
            "Connection source and target ports do not share a compatible role.",
            path);
        issues.Add(issue);
        connectionIssues.Add(issue);
        return WorkflowConnectionRoleCompatibilityStatus.IncompatibleRole;
    }

    private static WorkflowDynamicPortAnalysisStatus DynamicStatus(
        WorkflowEffectivePort? source,
        WorkflowEffectivePort? target,
        WorkflowPortCatalogStatus sourceStatus,
        WorkflowPortCatalogStatus targetStatus)
    {
        if (sourceStatus == WorkflowPortCatalogStatus.UnknownPort || targetStatus == WorkflowPortCatalogStatus.UnknownPort)
        {
            return WorkflowDynamicPortAnalysisStatus.Unresolved;
        }

        return source?.Origin == WorkflowEffectivePortOrigin.Dynamic || target?.Origin == WorkflowEffectivePortOrigin.Dynamic
            ? WorkflowDynamicPortAnalysisStatus.Resolved
            : WorkflowDynamicPortAnalysisStatus.NotDynamic;
    }

    private static void AnalyzeMultiplicity(WorkflowDocument workflow, IReadOnlyList<WorkflowConnectionAnalysis> connections, IssueSink issues)
    {
        foreach (IGrouping<(string ToNode, string ToPort), WorkflowConnectionAnalysis> group in connections
            .Where(static connection => connection.TargetPort is { AllowsMultiple: false } &&
                connection.RoleCompatibilityStatus == WorkflowConnectionRoleCompatibilityStatus.Compatible)
            .GroupBy(static connection => (connection.ToNode, connection.ToPort)))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            foreach (WorkflowConnectionAnalysis connection in group)
            {
                issues.Add(new(
                    WorkflowAnalysisCodes.PortMultiplicityViolation,
                    WorkflowAnalysisSeverity.Error,
                    $"Input port '{connection.ToPort}' on node '{connection.ToNode}' allows only one compatible incoming connection.",
                    Pointer("connections", connection.ConnectionIndex ?? 0, "to", "port"),
                    connection.ToNode));
            }
        }
    }

    private static void AnalyzeDuplicateEffectivePorts(WorkflowNode node, int nodeIndex, IReadOnlyList<WorkflowEffectivePort> ports, IssueSink issues)
    {
        foreach (IGrouping<string, WorkflowEffectivePort> group in ports.GroupBy(static port => port.Id, StringComparer.Ordinal))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            issues.Add(new(
                WorkflowAnalysisCodes.InvalidDynamicPortDeclaration,
                WorkflowAnalysisSeverity.Error,
                $"Effective port ID '{group.Key}' is declared more than once.",
                Pointer("nodes", nodeIndex),
                node.Id,
                node.Type));
        }
    }

    private static WorkflowResourceRequirementAnalysisStatus AggregateResourceStatus(IReadOnlyList<WorkflowResourceSlotAnalysis> slots)
    {
        if (slots.Count == 0)
        {
            return WorkflowResourceRequirementAnalysisStatus.Satisfied;
        }

        if (slots.Any(static slot => slot.Status == WorkflowResourceRequirementAnalysisStatus.MissingRequiredResource))
        {
            return WorkflowResourceRequirementAnalysisStatus.MissingRequiredResource;
        }

        if (slots.Any(static slot => slot.Status == WorkflowResourceRequirementAnalysisStatus.IncompatibleResourceKind))
        {
            return WorkflowResourceRequirementAnalysisStatus.IncompatibleResourceKind;
        }

        return slots.Any(static slot => slot.Status == WorkflowResourceRequirementAnalysisStatus.MissingRequiredCapability)
            ? WorkflowResourceRequirementAnalysisStatus.MissingRequiredCapability
            : WorkflowResourceRequirementAnalysisStatus.Satisfied;
    }

    private static IReadOnlyList<WorkflowAnalysisIssue> OrderIssues(IReadOnlyList<WorkflowAnalysisIssue> issues)
    {
        return Array.AsReadOnly([.. issues
            .OrderBy(static issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(static issue => issue.NodeId, StringComparer.Ordinal)
            .ThenBy(static issue => issue.NodeType, StringComparer.Ordinal)]);
    }

    private static void AddDynamicIssue(IssueSink issues, WorkflowNode node, string path, string message)
    {
        issues.Add(new(
            WorkflowAnalysisCodes.InvalidDynamicPortDeclaration,
            WorkflowAnalysisSeverity.Error,
            message,
            path,
            node.Id,
            node.Type));
    }

    private static bool ContainsReservedNonLiteral(JsonNode? value)
    {
        if (value is JsonObject obj)
        {
            if (obj.ContainsKey("$literal"))
            {
                return false;
            }

            if (obj.ContainsKey("$binding") || obj.ContainsKey("$expression") || obj.ContainsKey("$resource") || obj.ContainsKey("$locator"))
            {
                return true;
            }

            return obj.Any(property => ContainsReservedNonLiteral(property.Value));
        }

        return value is JsonArray array && array.Any(ContainsReservedNonLiteral);
    }

    private static bool TryResolvePointer(JsonNode? root, string pointer, out JsonNode? value)
    {
        value = root;
        if (root is null)
        {
            return false;
        }

        if (pointer.Length == 0)
        {
            return true;
        }

        if (!pointer.StartsWith("/", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        foreach (string rawToken in pointer[1..].Split('/'))
        {
            string token = rawToken.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (value is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(token, out value))
                {
                    return false;
                }
            }
            else if (value is JsonArray array)
            {
                if (!int.TryParse(token, out int index) || index < 0 || index >= array.Count)
                {
                    value = null;
                    return false;
                }

                value = array[index];
            }
            else
            {
                value = null;
                return false;
            }
        }

        return true;
    }

    private static string CatalogIdentity(IWorkflowNodeDefinitionCatalog catalog)
    {
        return catalog.GetType().FullName ?? catalog.GetType().Name;
    }

    private static string Pointer(params object[] parts)
    {
        return "/" + string.Join("/", parts.Select(static part => Escape(Convert.ToString(part, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));
    }

    private static string Pointer(string basePath, params object[] parts)
    {
        string normalizedBasePath = basePath.StartsWith("/", StringComparison.Ordinal) ? basePath : "/" + basePath;
        if (parts.Length == 0)
        {
            return normalizedBasePath;
        }

        return normalizedBasePath + "/" + string.Join("/", parts.Select(static part => Escape(Convert.ToString(part, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));
    }

    private static string Escape(string value)
    {
        return value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PortIdRegex();

    private sealed class IssueSink
    {
        private readonly int _maximumIssues;
        private bool _limitReported;

        public IssueSink(int maximumIssues)
        {
            _maximumIssues = maximumIssues;
        }

        public List<WorkflowAnalysisIssue> Issues { get; } = [];

        public void Add(WorkflowAnalysisIssue issue)
        {
            if (Issues.Count < _maximumIssues)
            {
                Issues.Add(issue);
                return;
            }

            if (_limitReported)
            {
                return;
            }

            _limitReported = true;
            Issues.Add(new(
                WorkflowAnalysisCodes.IssueLimitReached,
                WorkflowAnalysisSeverity.Error,
                "The analyzer reached the configured diagnostic limit.",
                string.Empty));
        }
    }

    private sealed record NodeState(
        WorkflowNode Node,
        int NodeIndex,
        WorkflowNodeAnalysis Analysis,
        IReadOnlyDictionary<string, WorkflowEffectivePort> Ports);
}
